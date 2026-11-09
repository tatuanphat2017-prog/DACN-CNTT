using DACN.Api.Auth;
using DACN.Api.Data;
using DACN.Api.Dtos;
using DACN.Api.Entities;
using DACN.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Controllers;

/// <summary>Lớp học phần. Danh sách tự lọc theo người gọi: Admin thấy tất cả, Lecturer thấy lớp mình dạy, Student thấy lớp mình học.</summary>
[ApiController]
[Route("api/sections")]
[Authorize]
public class SectionsController(AppDbContext db, ICurrentUser user, SectionAccessService access, GpaService gpa) : ApiControllerBase
{
    /// <summary>Danh sách lớp học phần, lọc theo học kỳ, môn học, có phân trang. Học kỳ mới nhất trước.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<SectionListItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int? semesterId, [FromQuery] int? courseId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.CourseSections.AsNoTracking();
        if (user.Role == UserRole.Lecturer)
        {
            var lecturerId = await user.GetLecturerIdAsync();
            query = query.Where(s => s.LecturerId == lecturerId);
        }
        else if (user.Role == UserRole.Student)
        {
            var studentId = await user.GetStudentIdAsync();
            query = query.Where(s => s.Enrollments.Any(e => e.StudentId == studentId));
        }
        if (semesterId is not null)
        {
            query = query.Where(s => s.SemesterId == semesterId);
        }
        if (courseId is not null)
        {
            query = query.Where(s => s.CourseId == courseId);
        }

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(s => s.Semester.StartDate).ThenBy(s => s.Code)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new SectionListItem(s.Id, s.Code, s.CourseId, s.Course.Code, s.Course.Name, s.SemesterId, s.Semester.Name,
                s.LecturerId, s.Lecturer.User.FullName, s.TotalSessions, s.Enrollments.Count))
            .ToListAsync();
        return Ok(new PagedResult<SectionListItem>(items, page, pageSize, total));
    }

    /// <summary>Chi tiết một lớp học phần, kèm trọng số điểm. Giảng viên chỉ xem lớp mình dạy, sinh viên chỉ xem lớp mình học (sai trả 403).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<SectionDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id)
    {
        var result = await access.GetAsync(id, allowStudent: true);
        if (Reject(result) is { } denied)
            return denied;
        return Ok(await LoadDetailAsync(id));
    }

    /// <summary>
    /// Mở lớp học phần. Tổng 4 trọng số phải bằng 1. Mã lớp trùng trả 409; môn, học kỳ, giảng viên, phòng không tồn tại trả 400.
    /// Lịch học (Schedule) không bắt buộc; trùng phòng hoặc trùng giảng viên cùng học kỳ, cùng thứ, giao tiết trả 409.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<SectionDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(UpsertSectionRequest request)
    {
        if (await Validate(request) is { } invalid)
            return invalid;
        var code = request.Code.Trim();
        if (await db.CourseSections.AnyAsync(s => s.Code == code))
            return ConflictProblem("Mã lớp học phần đã tồn tại");
        if (await ScheduleConflict(request, null) is { } conflict)
            return conflict;

        var section = new CourseSection { Code = code };
        Apply(section, request);
        db.CourseSections.Add(section);
        await db.SaveChangesAsync();
        return Created($"/api/sections/{section.Id}", await LoadDetailAsync(section.Id));
    }

    /// <summary>
    /// Sửa lớp học phần (thay toàn bộ: Schedule bỏ trống nghĩa là xóa lịch học và lịch thi). Không giảm tổng số buổi xuống thấp hơn
    /// số buổi đã điểm danh. Trùng phòng hoặc trùng giảng viên trả 409.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<SectionDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, UpsertSectionRequest request)
    {
        var section = await db.CourseSections.FirstOrDefaultAsync(s => s.Id == id);
        if (section is null)
            return NotFound();
        if (await Validate(request) is { } invalid)
            return invalid;
        var code = request.Code.Trim();
        if (await db.CourseSections.AnyAsync(s => s.Code == code && s.Id != id))
            return ConflictProblem("Mã lớp học phần đã tồn tại");

        if (await ScheduleConflict(request, id) is { } conflict)
            return conflict;

        var maxSession = await db.AttendanceSessions.Where(a => a.CourseSectionId == id).MaxAsync(a => (int?)a.SessionNo) ?? 0;
        if (request.TotalSessions < maxSession)
            return InvalidField("TotalSessions", $"Lớp đã điểm danh tới buổi {maxSession}, tổng số buổi không được nhỏ hơn");

        Apply(section, request);
        await db.SaveChangesAsync();
        return Ok(await LoadDetailAsync(id));
    }

    /// <summary>Xóa lớp học phần. Lớp còn sinh viên đăng ký thì trả 409 (xóa hết sinh viên khỏi lớp trước). Các buổi điểm danh còn sót được xóa theo.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var section = await db.CourseSections.FirstOrDefaultAsync(s => s.Id == id);
        if (section is null)
            return NotFound();
        if (await db.Enrollments.AnyAsync(e => e.CourseSectionId == id))
            return ConflictProblem("Lớp còn sinh viên đăng ký, không xóa được");

        db.CourseSections.Remove(section);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Danh sách sinh viên trong lớp (kèm EnrollmentId, dùng cho điểm danh, điểm và cảnh báo). Giảng viên chỉ xem lớp mình dạy.</summary>
    [HttpGet("{id:int}/students")]
    [Authorize(Roles = Roles.AdminOrLecturer)]
    [ProducesResponseType<IReadOnlyList<SectionStudent>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Students(int id)
    {
        if (Reject(await access.GetAsync(id)) is { } denied)
            return denied;
        return Ok(await db.Enrollments.AsNoTracking().Where(e => e.CourseSectionId == id)
            .OrderBy(e => e.Student.StudentCode)
            .Select(e => new SectionStudent(e.Id, e.StudentId, e.Student.StudentCode, e.Student.User.FullName, e.Student.ClassName))
            .ToListAsync());
    }

    /// <summary>
    /// Thêm sinh viên vào lớp. Sinh viên đã có trong lớp được bỏ qua, không báo lỗi. Id không tồn tại hoặc bị khóa trả 400.
    /// PriorGpa của từng sinh viên được tính tự động từ các học kỳ đã kết thúc trước học kỳ của lớp.
    /// </summary>
    [HttpPost("{id:int}/students")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<EnrollStudentsResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Enroll(int id, EnrollStudentsRequest request)
    {
        var section = await db.CourseSections.Include(s => s.Semester).FirstOrDefaultAsync(s => s.Id == id);
        if (section is null)
            return NotFound();

        var ids = request.StudentIds.Distinct().ToList();
        var valid = await db.Students.Where(s => ids.Contains(s.Id) && s.User.IsActive).Select(s => s.Id).ToListAsync();
        var missing = ids.Except(valid).ToList();
        if (missing.Count > 0)
            return InvalidField("StudentIds", $"Không tìm thấy sinh viên hợp lệ với id: {string.Join(", ", missing)}");

        var already = await db.Enrollments.Where(e => e.CourseSectionId == id && ids.Contains(e.StudentId)).Select(e => e.StudentId).ToListAsync();
        var toAdd = valid.Except(already).ToList();
        foreach (var studentId in toAdd)
        {
            db.Enrollments.Add(new Enrollment
            {
                CourseSectionId = id,
                StudentId = studentId,
                PriorGpa = await gpa.PriorGpaAsync(studentId, section.Semester.StartDate),
            });
        }
        await db.SaveChangesAsync();
        return Ok(new EnrollStudentsResult(toAdd.Count, already.Count));
    }

    /// <summary>Xóa sinh viên khỏi lớp (xóa luôn điểm danh, điểm, dự đoán rủi ro, cảnh báo của sinh viên trong lớp).</summary>
    [HttpDelete("{id:int}/students/{enrollmentId:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unenroll(int id, int enrollmentId)
    {
        var enrollment = await db.Enrollments.FirstOrDefaultAsync(e => e.Id == enrollmentId && e.CourseSectionId == id);
        if (enrollment is null)
            return NotFound();

        await db.InTransactionAsync(async () =>
        {
            // AttendanceRecords.EnrollmentId đặt Restrict (tránh multiple cascade paths) nên phải xóa tay trước
            await db.AttendanceRecords.Where(r => r.EnrollmentId == enrollmentId).ExecuteDeleteAsync();
            db.Enrollments.Remove(enrollment);
            await db.SaveChangesAsync();
            return true;
        });
        return NoContent();
    }

    private async Task<SectionDetail?> LoadDetailAsync(int id)
    {
        var detail = await db.CourseSections.AsNoTracking().Where(s => s.Id == id)
            .Select(s => new SectionDetail(s.Id, s.Code, s.CourseId, s.Course.Code, s.Course.Name, s.SemesterId, s.Semester.Name,
                s.LecturerId, s.Lecturer.User.FullName, s.TotalSessions,
                new SectionWeights(s.AttendanceWeight, s.AssignmentWeight, s.MidtermWeight, s.FinalWeight), s.Enrollments.Count, null))
            .FirstOrDefaultAsync();
        if (detail is null)
            return null;

        var section = await db.CourseSections.AsNoTracking().Include(s => s.Room).Include(s => s.ExamRoom).FirstAsync(s => s.Id == id);
        return detail with { Schedule = ToScheduleInfo(section) };
    }

    private static SectionScheduleInfo? ToScheduleInfo(CourseSection s)
    {
        if (s.Weekday is null && s.ExamDate is null)
            return null;
        TimeOnly? start = null, end = null;
        if (s is { StartPeriod: { } sp, PeriodCount: { } pc })
            (start, end) = Timetable.Range(sp, pc);
        return new SectionScheduleInfo(s.Weekday, s.StartPeriod, s.PeriodCount, start, end,
            s.RoomId, s.Room?.Code, s.Room?.Building, s.Room?.Floor,
            s.ExamDate, s.ExamStartTime, s.ExamRoomId, s.ExamRoom?.Code, s.ExamRoom?.Building, s.ExamRoom?.Floor);
    }

    /// <summary>Kiểm tra trùng phòng / trùng giảng viên (cùng học kỳ, cùng thứ, giao tiết). Trả 409 nếu trùng.</summary>
    private async Task<IActionResult?> ScheduleConflict(UpsertSectionRequest request, int? sectionId)
    {
        if (request.Schedule is not { Weekday: { } day, StartPeriod: { } start, PeriodCount: { } count, RoomId: { } roomId })
            return null;

        var end = start + count;     // tiết kết thúc (không tính)
        var overlapping = db.CourseSections.Where(o => o.Id != sectionId && o.SemesterId == request.SemesterId && o.Weekday == day
                                                       && o.StartPeriod < end && start < o.StartPeriod + o.PeriodCount);
        if (await overlapping.AnyAsync(o => o.RoomId == roomId))
            return ConflictProblem("Phòng học đã có lớp khác vào thứ và tiết này");
        if (await overlapping.AnyAsync(o => o.LecturerId == request.LecturerId))
            return ConflictProblem("Giảng viên đã có lớp khác vào thứ và tiết này");
        return null;
    }

    private async Task<ActionResult?> Validate(UpsertSectionRequest request)
    {
        var w = request.Weights;
        if (new[] { w.AttendanceWeight, w.AssignmentWeight, w.MidtermWeight, w.FinalWeight }.Any(x => x is < 0 or > 1))
            return InvalidField("Weights", "Mỗi trọng số phải trong khoảng 0–1");
        if (w.AttendanceWeight + w.AssignmentWeight + w.MidtermWeight + w.FinalWeight != 1m)
            return InvalidField("Weights", "Tổng 4 trọng số phải bằng 1");
        if (!await db.Courses.AnyAsync(c => c.Id == request.CourseId))
            return InvalidField("CourseId", "Môn học không tồn tại");
        if (!await db.Semesters.AnyAsync(s => s.Id == request.SemesterId))
            return InvalidField("SemesterId", "Học kỳ không tồn tại");
        if (!await db.Lecturers.AnyAsync(l => l.Id == request.LecturerId && l.User.IsActive))
            return InvalidField("LecturerId", "Giảng viên không tồn tại hoặc đã bị khóa");
        return await ValidateSchedule(request.Schedule);
    }

    private async Task<ActionResult?> ValidateSchedule(SectionScheduleRequest? s)
    {
        if (s is null)
            return null;

        var given = new object?[] { s.Weekday, s.StartPeriod, s.PeriodCount, s.RoomId }.Count(x => x is not null);
        if (given is not (0 or 4))
            return InvalidField("Schedule", "Lịch học phải điền đủ Weekday, StartPeriod, PeriodCount, RoomId hoặc bỏ trống cả bốn");
        if (given == 4)
        {
            if (s.Weekday is < 2 or > 8)
                return InvalidField("Schedule.Weekday", "Thứ phải từ 2 (Thứ hai) đến 8 (Chủ nhật)");
            if (s.StartPeriod is < 1 or > Timetable.PeriodsPerDay || s.PeriodCount < 1 || s.StartPeriod + s.PeriodCount - 1 > Timetable.PeriodsPerDay)
                return InvalidField("Schedule.StartPeriod", $"Tiết học phải nằm trong khoảng 1–{Timetable.PeriodsPerDay}");
            if (!await db.Rooms.AnyAsync(r => r.Id == s.RoomId))
                return InvalidField("Schedule.RoomId", "Phòng học không tồn tại");
        }
        if ((s.ExamStartTime is not null || s.ExamRoomId is not null) && s.ExamDate is null)
            return InvalidField("Schedule.ExamDate", "Phải có ngày thi khi đã nhập giờ hoặc phòng thi");
        if (s.ExamRoomId is { } examRoom && !await db.Rooms.AnyAsync(r => r.Id == examRoom))
            return InvalidField("Schedule.ExamRoomId", "Phòng thi không tồn tại");
        return null;
    }

    private static void Apply(CourseSection section, UpsertSectionRequest request)
    {
        section.Code = request.Code.Trim();
        section.CourseId = request.CourseId;
        section.SemesterId = request.SemesterId;
        section.LecturerId = request.LecturerId;
        section.TotalSessions = request.TotalSessions;
        section.AttendanceWeight = request.Weights.AttendanceWeight;
        section.AssignmentWeight = request.Weights.AssignmentWeight;
        section.MidtermWeight = request.Weights.MidtermWeight;
        section.FinalWeight = request.Weights.FinalWeight;

        var schedule = request.Schedule;
        section.Weekday = schedule?.Weekday;
        section.StartPeriod = schedule?.StartPeriod;
        section.PeriodCount = schedule?.PeriodCount;
        section.RoomId = schedule?.RoomId;
        section.ExamDate = schedule?.ExamDate;
        section.ExamStartTime = schedule?.ExamStartTime;
        section.ExamRoomId = schedule?.ExamRoomId;
    }
}

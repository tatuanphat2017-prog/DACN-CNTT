using DACN.Api.Auth;
using DACN.Api.Data;
using DACN.Api.Dtos;
using DACN.Api.Entities;
using DACN.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Controllers;

/// <summary>Cổng sinh viên: dữ liệu của chính người đang đăng nhập. Chỉ Student.</summary>
[ApiController]
[Route("api/students/me")]
[Authorize(Roles = Roles.Student)]
public class StudentPortalController(AppDbContext db, ICurrentUser user, RiskService risk) : ApiControllerBase
{
    /// <summary>Các lớp sinh viên đang học (mới nhất trước), kèm số buổi vắng, điểm tổng và mức rủi ro hiện tại.</summary>
    [HttpGet("sections")]
    [ProducesResponseType<IReadOnlyList<MySectionItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Sections()
    {
        if (await user.GetStudentIdAsync() is not { } studentId)
            return Forbid();

        return Ok(await db.Enrollments.AsNoTracking().Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.CourseSection.Semester.StartDate).ThenBy(e => e.CourseSection.Code)
            .Select(e => new MySectionItem(e.CourseSectionId, e.CourseSection.Code, e.CourseSection.Course.Name,
                e.CourseSection.Semester.Name, e.CourseSection.Lecturer.User.FullName,
                e.AttendanceRecords.Count(r => r.Status == AttendanceStatus.Absent), e.CourseSection.TotalSessions, e.TotalScore,
                e.RiskPredictions.OrderByDescending(p => p.PredictedAt).Select(p => (RiskLevel?)p.RiskLevel).FirstOrDefault()))
            .ToListAsync());
    }

    /// <summary>Chi tiết một lớp của sinh viên: các điểm thành phần, trọng số, lịch sử điểm danh. Lớp không đăng ký trả 404.</summary>
    [HttpGet("sections/{sectionId:int}")]
    [ProducesResponseType<MySectionDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Section(int sectionId)
    {
        if (await user.GetStudentIdAsync() is not { } studentId)
            return Forbid();

        var detail = await db.Enrollments.AsNoTracking().Where(e => e.StudentId == studentId && e.CourseSectionId == sectionId)
            .Select(e => new
            {
                e.Id,
                Item = new MySectionItem(e.CourseSectionId, e.CourseSection.Code, e.CourseSection.Course.Name,
                    e.CourseSection.Semester.Name, e.CourseSection.Lecturer.User.FullName,
                    e.AttendanceRecords.Count(r => r.Status == AttendanceStatus.Absent), e.CourseSection.TotalSessions, e.TotalScore,
                    e.RiskPredictions.OrderByDescending(p => p.PredictedAt).Select(p => (RiskLevel?)p.RiskLevel).FirstOrDefault()),
                Weights = new SectionWeights(e.CourseSection.AttendanceWeight, e.CourseSection.AssignmentWeight,
                    e.CourseSection.MidtermWeight, e.CourseSection.FinalWeight),
                e.AttendanceScore, e.AssignmentScore, e.MidtermScore, e.FinalScore, e.TotalScore, e.IsPassed,
            })
            .FirstOrDefaultAsync();
        if (detail is null)
            return NotFound();

        var attendance = await db.AttendanceRecords.AsNoTracking().Where(r => r.EnrollmentId == detail.Id)
            .OrderBy(r => r.AttendanceSession.SessionNo)
            .Select(r => new MyAttendanceRecord(r.AttendanceSession.SessionNo, r.AttendanceSession.Date, r.Status))
            .ToListAsync();
        return Ok(new MySectionDetail(detail.Item, detail.Weights, detail.AttendanceScore, detail.AssignmentScore,
            detail.MidtermScore, detail.FinalScore, detail.TotalScore, detail.IsPassed, attendance));
    }

    /// <summary>
    /// Mức rủi ro của sinh viên ở từng lớp (kết quả dự đoán mới nhất), kèm các yếu tố chính và gợi ý cải thiện. Xác suất cao trước.
    /// Lớp chưa được dự đoán thì không có trong danh sách.
    /// </summary>
    [HttpGet("risks")]
    [ProducesResponseType<IReadOnlyList<MyRisk>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Risks()
    {
        if (await user.GetStudentIdAsync() is not { } studentId)
            return Forbid();

        var rows = await risk.Latest().AsNoTracking().Where(p => p.Enrollment.StudentId == studentId)
            .OrderByDescending(p => p.Probability)
            .Select(p => new
            {
                p.Enrollment.CourseSectionId, p.Enrollment.CourseSection.Code, p.Enrollment.CourseSection.Course.Name,
                p.Probability, p.RiskLevel, p.TopFactors, p.PredictedAt,
            })
            .ToListAsync();

        return Ok(rows.Select(r =>
        {
            var factors = RiskService.ParseFactors(r.TopFactors);
            return new MyRisk(r.CourseSectionId, r.Code, r.Name, r.Probability, r.RiskLevel, factors, RiskText.Suggestions(r.RiskLevel, factors), r.PredictedAt);
        }).ToList());
    }

    /// <summary>
    /// Thời khóa biểu hằng tuần của một học kỳ: môn, giảng viên, thứ, tiết, giờ học, phòng, tòa, lầu. Sắp theo thứ rồi tiết.
    /// Không truyền semesterId thì lấy học kỳ hiện tại (nếu sinh viên không học học kỳ đó thì lấy học kỳ gần nhất có đăng ký). Lớp chưa xếp lịch không có trong danh sách.
    /// </summary>
    [HttpGet("timetable")]
    [ProducesResponseType<MyTimetable>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTimetable([FromQuery] int? semesterId)
    {
        if (await user.GetStudentIdAsync() is not { } studentId)
            return Forbid();
        if (await ResolveSemesterAsync(studentId, semesterId) is not { } semester)
            return NotFound();

        var rows = await db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == studentId && e.CourseSection.SemesterId == semester.Id && e.CourseSection.Weekday != null)
            .OrderBy(e => e.CourseSection.Weekday).ThenBy(e => e.CourseSection.StartPeriod)
            .Select(e => new
            {
                e.CourseSectionId, e.CourseSection.Code, CourseCode = e.CourseSection.Course.Code, e.CourseSection.Course.Name,
                Lecturer = e.CourseSection.Lecturer.User.FullName, e.CourseSection.Weekday, e.CourseSection.StartPeriod,
                e.CourseSection.PeriodCount, Room = e.CourseSection.Room!.Code, e.CourseSection.Room.Building, e.CourseSection.Room.Floor,
            })
            .ToListAsync();

        var items = rows.Select(r =>
        {
            var (start, end) = Timetable.Range(r.StartPeriod!.Value, r.PeriodCount!.Value);
            return new TimetableItem(r.CourseSectionId, r.Code, r.CourseCode, r.Name, r.Lecturer, r.Weekday!.Value,
                r.StartPeriod.Value, r.PeriodCount.Value, start, end, r.Room, r.Building, r.Floor);
        }).ToList();
        return Ok(new MyTimetable(semester.Id, semester.Name, Timetable.AcademicYear(semester.StartDate), items));
    }

    /// <summary>
    /// Lịch học theo ngày trong khoảng from..to (mặc định tuần hiện tại, thứ hai đến chủ nhật; tối đa 92 ngày). Mỗi dòng là một buổi học cụ thể
    /// với số buổi (SessionNo) của lớp trong học kỳ. Chỉ tính các ngày nằm trong học kỳ và không vượt tổng số buổi của lớp.
    /// </summary>
    [HttpGet("schedule")]
    [ProducesResponseType<IReadOnlyList<ScheduleItem>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSchedule([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        if (await user.GetStudentIdAsync() is not { } studentId)
            return Forbid();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var start = from ?? today.AddDays(-(Timetable.Weekday(today) - 2));
        var end = to ?? start.AddDays(6);
        if (end < start)
            return InvalidField("to", "Ngày kết thúc phải sau hoặc bằng ngày bắt đầu");
        if (end.DayNumber - start.DayNumber > 91)
            return InvalidField("to", "Khoảng thời gian tối đa 92 ngày");

        var sections = await db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == studentId && e.CourseSection.Weekday != null
                        && e.CourseSection.Semester.StartDate <= end && e.CourseSection.Semester.EndDate >= start)
            .Select(e => new
            {
                e.CourseSectionId, e.CourseSection.Code, e.CourseSection.Course.Name, Lecturer = e.CourseSection.Lecturer.User.FullName,
                e.CourseSection.Weekday, e.CourseSection.StartPeriod, e.CourseSection.PeriodCount, e.CourseSection.TotalSessions,
                SemesterStart = e.CourseSection.Semester.StartDate, SemesterEnd = e.CourseSection.Semester.EndDate,
                Room = e.CourseSection.Room!.Code, e.CourseSection.Room.Building, e.CourseSection.Room.Floor,
            })
            .ToListAsync();

        var items = new List<ScheduleItem>();
        foreach (var s in sections)
        {
            var (startTime, endTime) = Timetable.Range(s.StartPeriod!.Value, s.PeriodCount!.Value);
            var first = s.SemesterStart;
            while (Timetable.Weekday(first) != s.Weekday)
                first = first.AddDays(1);

            // Buổi đầu tiên không sớm hơn ngày bắt đầu khoảng tra cứu, cách buổi học đầu của học kỳ một số nguyên tuần
            var date = start > first ? first.AddDays((start.DayNumber - first.DayNumber + 6) / 7 * 7) : first;
            for (; date <= end && date <= s.SemesterEnd; date = date.AddDays(7))
            {
                var sessionNo = (date.DayNumber - first.DayNumber) / 7 + 1;
                if (sessionNo > s.TotalSessions)
                    break;
                items.Add(new ScheduleItem(date, s.Weekday!.Value, sessionNo, s.CourseSectionId, s.Code, s.Name, s.Lecturer,
                    startTime, endTime, s.Room, s.Building, s.Floor));
            }
        }
        return Ok(items.OrderBy(i => i.Date).ThenBy(i => i.StartTime).ToList());
    }

    /// <summary>
    /// Điểm thi cuối kỳ và lịch thi (ngày, giờ, phòng, tòa, lầu) của từng lớp. Lọc theo semesterId nếu muốn; mặc định tất cả học kỳ, mới nhất trước.
    /// </summary>
    [HttpGet("exams")]
    [ProducesResponseType<IReadOnlyList<ExamItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExams([FromQuery] int? semesterId)
    {
        if (await user.GetStudentIdAsync() is not { } studentId)
            return Forbid();

        var query = db.Enrollments.AsNoTracking().Where(e => e.StudentId == studentId);
        if (semesterId is not null)
            query = query.Where(e => e.CourseSection.SemesterId == semesterId);

        var rows = await query
            .OrderByDescending(e => e.CourseSection.Semester.StartDate).ThenBy(e => e.CourseSection.ExamDate).ThenBy(e => e.CourseSection.Code)
            .Select(e => new
            {
                e.CourseSectionId, e.CourseSection.Code, e.CourseSection.Course.Name, e.CourseSection.Course.Credits,
                Semester = e.CourseSection.Semester.Name, SemesterStart = e.CourseSection.Semester.StartDate,
                e.CourseSection.ExamDate, e.CourseSection.ExamStartTime,
                Room = e.CourseSection.ExamRoom!.Code, Building = e.CourseSection.ExamRoom.Building, Floor = (int?)e.CourseSection.ExamRoom.Floor,
                e.FinalScore, e.TotalScore, e.IsPassed,
            })
            .ToListAsync();

        return Ok(rows.Select(r => new ExamItem(r.CourseSectionId, r.Code, r.Name, r.Credits, r.Semester,
            Timetable.AcademicYear(r.SemesterStart), r.ExamDate, r.ExamStartTime, r.Room, r.Building, r.Floor,
            r.FinalScore, r.TotalScore, r.IsPassed)).ToList());
    }

    /// <summary>
    /// Bảng điểm toàn khóa: điểm thành phần và điểm tổng từng môn (kèm điểm quy đổi thang 4), GPA từng học kỳ, GPA từng năm học và GPA tích lũy.
    /// GPA tính theo tín chỉ; môn không đạt tính 0; môn đang học (chưa có kết quả) không tính vào GPA.
    /// </summary>
    [HttpGet("transcript")]
    [ProducesResponseType<Transcript>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTranscript()
    {
        if (await user.GetStudentIdAsync() is not { } studentId)
            return Forbid();

        var info = await db.Students.AsNoTracking().Where(s => s.Id == studentId)
            .Select(s => new { s.StudentCode, s.User.FullName }).FirstAsync();
        var rows = await db.Enrollments.AsNoTracking().Where(e => e.StudentId == studentId)
            .Select(e => new
            {
                SemesterId = e.CourseSection.SemesterId, Semester = e.CourseSection.Semester.Name, e.CourseSection.Semester.StartDate,
                e.CourseSectionId, e.CourseSection.Code, CourseCode = e.CourseSection.Course.Code, e.CourseSection.Course.Name,
                e.CourseSection.Course.Credits, e.AttendanceScore, e.AssignmentScore, e.MidtermScore, e.FinalScore, e.TotalScore, e.IsPassed,
            })
            .ToListAsync();

        static (decimal, bool, int) Result(decimal? total, bool? passed, int credits) => (total ?? 0m, passed == true, credits);

        var semesters = rows.GroupBy(r => new { r.SemesterId, r.Semester, r.StartDate })
            .OrderByDescending(g => g.Key.StartDate)
            .Select(g =>
            {
                var graded = g.Where(r => r.IsPassed is not null).ToList();
                var courses = g.OrderBy(r => r.Code).Select(r => new TranscriptCourse(
                    r.CourseSectionId, r.Code, r.CourseCode, r.Name, r.Credits,
                    r.AttendanceScore, r.AssignmentScore, r.MidtermScore, r.FinalScore, r.TotalScore,
                    r.IsPassed is null ? null : r.IsPassed == true ? GradeScale.ToGradePoint(r.TotalScore ?? 0m) : 0m,
                    r.IsPassed is null ? "Đang học" : r.IsPassed == true ? "Đạt" : "Không đạt")).ToList();
                return new TranscriptSemester(g.Key.SemesterId, g.Key.Semester, Timetable.AcademicYear(g.Key.StartDate),
                    graded.Count == g.Count(), GradeScale.Gpa(graded.Select(r => Result(r.TotalScore, r.IsPassed, r.Credits))),
                    g.Sum(r => r.Credits), graded.Where(r => r.IsPassed == true).Sum(r => r.Credits), courses);
            })
            .ToList();

        var allGraded = rows.Where(r => r.IsPassed is not null).ToList();
        var years = allGraded.GroupBy(r => Timetable.AcademicYear(r.StartDate))
            .OrderByDescending(g => g.Key)
            .Select(g => new TranscriptYear(g.Key, GradeScale.Gpa(g.Select(r => Result(r.TotalScore, r.IsPassed, r.Credits))),
                g.Where(r => r.IsPassed == true).Sum(r => r.Credits)))
            .ToList();

        return Ok(new Transcript(info.StudentCode, info.FullName,
            GradeScale.Gpa(allGraded.Select(r => Result(r.TotalScore, r.IsPassed, r.Credits))),
            allGraded.Where(r => r.IsPassed == true).Sum(r => r.Credits), years, semesters));
    }

    /// <summary>Học kỳ được chọn: semesterId nếu có; không thì học kỳ hiện tại mà sinh viên có học, không có thì học kỳ gần nhất có đăng ký.</summary>
    private async Task<Semester?> ResolveSemesterAsync(int studentId, int? semesterId)
    {
        if (semesterId is not null)
            return await db.Semesters.AsNoTracking().FirstOrDefaultAsync(s => s.Id == semesterId);

        return await db.Enrollments.AsNoTracking().Where(e => e.StudentId == studentId)
            .Select(e => e.CourseSection.Semester).Distinct()
            .OrderByDescending(s => s.IsCurrent).ThenByDescending(s => s.StartDate)
            .FirstOrDefaultAsync();
    }
}

using DACN.Api.Auth;
using DACN.Api.Dtos;
using DACN.Api.Mock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DACN.Api.Controllers;

/// <summary>Lớp học phần. Danh sách tự lọc theo người gọi: Admin thấy tất cả, Lecturer thấy lớp mình dạy, Student thấy lớp mình học.</summary>
[ApiController]
[Route("api/sections")]
[Authorize]
[Mock]
public class SectionsController : ControllerBase
{
    /// <summary>Danh sách lớp học phần, lọc theo học kỳ, môn học, có phân trang.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<SectionListItem>>(StatusCodes.Status200OK)]
    public IActionResult List([FromQuery] int? semesterId, [FromQuery] int? courseId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var items = MockData.Sections.AsEnumerable();
        if (semesterId is not null) items = items.Where(s => s.SemesterId == semesterId);
        if (courseId is not null) items = items.Where(s => s.CourseId == courseId);
        return Ok(MockData.Page(items, page, pageSize));
    }

    /// <summary>Chi tiết một lớp học phần, kèm trọng số điểm.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<SectionDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Get(int id) => MockData.Section(id) is { } s ? Ok(MockData.SectionDetail(s)) : NotFound();

    /// <summary>Mở lớp học phần. Tổng 4 trọng số phải bằng 1. Mã lớp trùng trả 409.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<SectionDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Create(UpsertSectionRequest request)
    {
        var w = request.Weights;
        if (w.AttendanceWeight + w.AssignmentWeight + w.MidtermWeight + w.FinalWeight != 1m)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["Weights"] = ["Tổng 4 trọng số phải bằng 1"] }));
        if (MockData.Sections.Any(s => s.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase)))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Mã lớp học phần đã tồn tại");

        var course = MockData.Courses.FirstOrDefault(c => c.Id == request.CourseId);
        var semester = MockData.Semesters.FirstOrDefault(s => s.Id == request.SemesterId);
        var lecturer = MockData.Lecturers.FirstOrDefault(l => l.Id == request.LecturerId);
        var created = new SectionDetail(100, request.Code, request.CourseId, course?.Code ?? "", course?.Name ?? "",
            request.SemesterId, semester?.Name ?? "", request.LecturerId, lecturer?.FullName ?? "",
            request.TotalSessions, w, 0);
        return Created("/api/sections/100", created);
    }

    /// <summary>Sửa lớp học phần.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<SectionDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Update(int id, UpsertSectionRequest request) =>
        MockData.Section(id) is { } s ? Ok(MockData.SectionDetail(s) with { Code = request.Code, TotalSessions = request.TotalSessions, Weights = request.Weights }) : NotFound();

    /// <summary>Xóa lớp học phần. Lớp đã có sinh viên đăng ký hoặc điểm danh thì trả 409.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Delete(int id) =>
        MockData.Section(id) is null ? NotFound()
        : Problem(statusCode: StatusCodes.Status409Conflict, title: "Lớp đã có sinh viên, không xóa được");

    /// <summary>Danh sách sinh viên trong lớp (kèm EnrollmentId, dùng cho điểm danh, điểm và cảnh báo).</summary>
    [HttpGet("{id:int}/students")]
    [Authorize(Roles = Roles.AdminOrLecturer)]
    [ProducesResponseType<IReadOnlyList<SectionStudent>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Students(int id) => MockData.Section(id) is null ? NotFound() : Ok(MockData.SectionStudents());

    /// <summary>Thêm sinh viên vào lớp. Sinh viên đã có trong lớp được bỏ qua, không báo lỗi.</summary>
    [HttpPost("{id:int}/students")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<EnrollStudentsResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Enroll(int id, EnrollStudentsRequest request)
    {
        if (MockData.Section(id) is null) return NotFound();
        var existing = MockData.SectionStudents().Select(s => s.StudentId).ToHashSet();
        var already = request.StudentIds.Distinct().Count(existing.Contains);
        return Ok(new EnrollStudentsResult(request.StudentIds.Distinct().Count() - already, already));
    }

    /// <summary>Xóa sinh viên khỏi lớp (xóa luôn điểm danh, điểm, dự đoán rủi ro, cảnh báo của sinh viên trong lớp).</summary>
    [HttpDelete("{id:int}/students/{enrollmentId:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Unenroll(int id, int enrollmentId) =>
        MockData.Section(id) is null || !MockData.SectionStudents().Any(s => s.EnrollmentId == enrollmentId) ? NotFound() : NoContent();
}

/// <summary>Nhập dữ liệu từ file Excel. Chỉ Admin.</summary>
[ApiController]
[Route("api/import")]
[Authorize(Roles = Roles.Admin)]
[Mock]
public class ImportController : ControllerBase
{
    /// <summary>
    /// Nhập danh sách sinh viên từ file .xlsx (gửi dạng multipart/form-data, field tên "file").
    /// Cột bắt buộc: MSSV, Họ tên, Mã khoa, Lớp, Năm nhập học. Dòng lỗi được báo trong Errors, các dòng đúng vẫn được tạo.
    /// </summary>
    [HttpPost("students")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ImportResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public IActionResult ImportStudents(IFormFile file)
    {
        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["file"] = ["Chỉ nhận file .xlsx"] }));

        return Ok(new ImportResult(50, 47, 2, [new ImportError(12, "MSSV SV012 đã tồn tại"), new ImportError(31, "Mã khoa KHONGCO không hợp lệ")]));
    }
}

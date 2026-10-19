using DACN.Api.Auth;
using DACN.Api.Dtos;
using DACN.Api.Mock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DACN.Api.Controllers;

/// <summary>Khoa. Mọi người dùng đăng nhập xem được, chỉ Admin thêm/sửa/xóa.</summary>
[ApiController]
[Route("api/departments")]
[Authorize]
[Mock]
public class DepartmentsController : ControllerBase
{
    /// <summary>Danh sách khoa.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DepartmentDto>>(StatusCodes.Status200OK)]
    public IActionResult List() => Ok(MockData.Departments);

    /// <summary>Thêm khoa. Mã khoa trùng trả 409.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Create(UpsertDepartmentRequest request) =>
        MockData.Departments.Any(d => d.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase))
            ? Problem(statusCode: StatusCodes.Status409Conflict, title: "Mã khoa đã tồn tại")
            : Created("/api/departments/100", new DepartmentDto(100, request.Code, request.Name));

    /// <summary>Sửa khoa.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Update(int id, UpsertDepartmentRequest request) =>
        MockData.Departments.Any(d => d.Id == id) ? Ok(new DepartmentDto(id, request.Code, request.Name)) : NotFound();

    /// <summary>Xóa khoa. Còn môn học, sinh viên hoặc giảng viên thuộc khoa thì trả 409.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Delete(int id) =>
        !MockData.Departments.Any(d => d.Id == id) ? NotFound()
        : id == 1 ? Problem(statusCode: StatusCodes.Status409Conflict, title: "Khoa còn dữ liệu liên quan, không xóa được")
        : NoContent();
}

/// <summary>Môn học. Mọi người dùng đăng nhập xem được, chỉ Admin thêm/sửa/xóa.</summary>
[ApiController]
[Route("api/courses")]
[Authorize]
[Mock]
public class CoursesController : ControllerBase
{
    /// <summary>Danh sách môn học, lọc theo khoa và từ khóa (mã hoặc tên), có phân trang.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<CourseDto>>(StatusCodes.Status200OK)]
    public IActionResult List([FromQuery] int? departmentId, [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var items = MockData.Courses.AsEnumerable();
        if (departmentId is not null) items = items.Where(c => c.DepartmentId == departmentId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            items = items.Where(c => c.Code.Contains(q, StringComparison.OrdinalIgnoreCase)
                                     || c.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        return Ok(MockData.Page(items, page, pageSize));
    }

    /// <summary>Thêm môn học. Mã môn trùng trả 409.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<CourseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Create(UpsertCourseRequest request)
    {
        if (MockData.Courses.Any(c => c.Code.Equals(request.Code, StringComparison.OrdinalIgnoreCase)))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Mã môn học đã tồn tại");
        var department = MockData.Departments.FirstOrDefault(d => d.Id == request.DepartmentId)?.Name ?? "";
        return Created("/api/courses/100", new CourseDto(100, request.Code, request.Name, request.Credits, request.DepartmentId, department));
    }

    /// <summary>Sửa môn học.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<CourseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Update(int id, UpsertCourseRequest request)
    {
        if (!MockData.Courses.Any(c => c.Id == id)) return NotFound();
        var department = MockData.Departments.FirstOrDefault(d => d.Id == request.DepartmentId)?.Name ?? "";
        return Ok(new CourseDto(id, request.Code, request.Name, request.Credits, request.DepartmentId, department));
    }

    /// <summary>Xóa môn học. Môn đã mở lớp thì trả 409.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Delete(int id) =>
        !MockData.Courses.Any(c => c.Id == id) ? NotFound()
        : Problem(statusCode: StatusCodes.Status409Conflict, title: "Môn học đã mở lớp, không xóa được");
}

/// <summary>Học kỳ. Mọi người dùng đăng nhập xem được, chỉ Admin thêm/sửa/xóa.</summary>
[ApiController]
[Route("api/semesters")]
[Authorize]
[Mock]
public class SemestersController : ControllerBase
{
    /// <summary>Danh sách học kỳ.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SemesterDto>>(StatusCodes.Status200OK)]
    public IActionResult List() => Ok(MockData.Semesters);

    /// <summary>Học kỳ hiện tại (IsCurrent = true).</summary>
    [HttpGet("current")]
    [ProducesResponseType<SemesterDto>(StatusCodes.Status200OK)]
    public IActionResult Current() => Ok(MockData.Semesters.First(s => s.IsCurrent));

    /// <summary>Thêm học kỳ. Ngày kết thúc phải sau ngày bắt đầu.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<SemesterDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public IActionResult Create(UpsertSemesterRequest request) =>
        request.EndDate <= request.StartDate
            ? ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["EndDate"] = ["Ngày kết thúc phải sau ngày bắt đầu"] }))
            : Created("/api/semesters/100", new SemesterDto(100, request.Name, request.StartDate, request.EndDate, request.IsCurrent));

    /// <summary>Sửa học kỳ.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<SemesterDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Update(int id, UpsertSemesterRequest request) =>
        MockData.Semesters.Any(s => s.Id == id)
            ? Ok(new SemesterDto(id, request.Name, request.StartDate, request.EndDate, request.IsCurrent))
            : NotFound();

    /// <summary>Xóa học kỳ. Học kỳ đã mở lớp thì trả 409.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Delete(int id) =>
        !MockData.Semesters.Any(s => s.Id == id) ? NotFound()
        : Problem(statusCode: StatusCodes.Status409Conflict, title: "Học kỳ đã mở lớp, không xóa được");
}

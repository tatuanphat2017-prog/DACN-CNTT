using DACN.Api.Auth;
using DACN.Api.Data;
using DACN.Api.Dtos;
using DACN.Api.Entities;
using DACN.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Controllers;

/// <summary>Khoa. Mọi người dùng đăng nhập xem được, chỉ Admin thêm/sửa/xóa.</summary>
[ApiController]
[Route("api/departments")]
[Authorize]
public class DepartmentsController(AppDbContext db) : ApiControllerBase
{
    /// <summary>Danh sách khoa.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DepartmentDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List() =>
        Ok(await db.Departments.AsNoTracking().OrderBy(d => d.Code).Select(d => new DepartmentDto(d.Id, d.Code, d.Name)).ToListAsync());

    /// <summary>Thêm khoa. Mã khoa trùng trả 409.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(UpsertDepartmentRequest request)
    {
        var code = request.Code.Trim();
        if (await db.Departments.AnyAsync(d => d.Code == code))
            return ConflictProblem("Mã khoa đã tồn tại");

        var department = new Department { Code = code, Name = request.Name.Trim() };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        return Created($"/api/departments/{department.Id}", new DepartmentDto(department.Id, department.Code, department.Name));
    }

    /// <summary>Sửa khoa.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, UpsertDepartmentRequest request)
    {
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Id == id);
        if (department is null)
            return NotFound();

        var code = request.Code.Trim();
        if (await db.Departments.AnyAsync(d => d.Code == code && d.Id != id))
            return ConflictProblem("Mã khoa đã tồn tại");

        department.Code = code;
        department.Name = request.Name.Trim();
        await db.SaveChangesAsync();
        return Ok(new DepartmentDto(department.Id, department.Code, department.Name));
    }

    /// <summary>Xóa khoa. Còn môn học, sinh viên hoặc giảng viên thuộc khoa thì trả 409.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Id == id);
        if (department is null)
            return NotFound();

        if (await db.Courses.AnyAsync(c => c.DepartmentId == id) || await db.Students.AnyAsync(s => s.DepartmentId == id)
            || await db.Lecturers.AnyAsync(l => l.DepartmentId == id))
            return ConflictProblem("Khoa còn dữ liệu liên quan, không xóa được");

        db.Departments.Remove(department);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

/// <summary>Môn học. Mọi người dùng đăng nhập xem được, chỉ Admin thêm/sửa/xóa.</summary>
[ApiController]
[Route("api/courses")]
[Authorize]
public class CoursesController(AppDbContext db) : ApiControllerBase
{
    /// <summary>Danh sách môn học, lọc theo khoa và từ khóa (mã hoặc tên), có phân trang.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<CourseDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int? departmentId, [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Courses.AsNoTracking();
        if (departmentId is not null)
        {
            query = query.Where(c => c.DepartmentId == departmentId);
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c => c.Code.Contains(term) || c.Name.Contains(term));
        }

        var total = await query.CountAsync();
        var items = await query.OrderBy(c => c.Code).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new CourseDto(c.Id, c.Code, c.Name, c.Credits, c.DepartmentId, c.Department.Name))
            .ToListAsync();
        return Ok(new PagedResult<CourseDto>(items, page, pageSize, total));
    }

    /// <summary>Thêm môn học. Mã môn trùng trả 409, khoa không tồn tại trả 400.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<CourseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(UpsertCourseRequest request)
    {
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Id == request.DepartmentId);
        if (department is null)
            return InvalidField("DepartmentId", "Khoa không tồn tại");
        var code = request.Code.Trim();
        if (await db.Courses.AnyAsync(c => c.Code == code))
            return ConflictProblem("Mã môn học đã tồn tại");

        var course = new Course { Code = code, Name = request.Name.Trim(), Credits = request.Credits, DepartmentId = department.Id };
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        return Created($"/api/courses/{course.Id}", ToDto(course, department.Name));
    }

    /// <summary>Sửa môn học.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<CourseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, UpsertCourseRequest request)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == id);
        if (course is null)
            return NotFound();
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Id == request.DepartmentId);
        if (department is null)
            return InvalidField("DepartmentId", "Khoa không tồn tại");
        var code = request.Code.Trim();
        if (await db.Courses.AnyAsync(c => c.Code == code && c.Id != id))
            return ConflictProblem("Mã môn học đã tồn tại");

        course.Code = code;
        course.Name = request.Name.Trim();
        course.Credits = request.Credits;
        course.DepartmentId = department.Id;
        await db.SaveChangesAsync();
        return Ok(ToDto(course, department.Name));
    }

    /// <summary>Xóa môn học. Môn đã mở lớp thì trả 409.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == id);
        if (course is null)
            return NotFound();
        if (await db.CourseSections.AnyAsync(s => s.CourseId == id))
            return ConflictProblem("Môn học đã mở lớp, không xóa được");

        db.Courses.Remove(course);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static CourseDto ToDto(Course c, string departmentName) =>
        new(c.Id, c.Code, c.Name, c.Credits, c.DepartmentId, departmentName);
}

/// <summary>Học kỳ. Mọi người dùng đăng nhập xem được, chỉ Admin thêm/sửa/xóa.</summary>
[ApiController]
[Route("api/semesters")]
[Authorize]
public class SemestersController(AppDbContext db) : ApiControllerBase
{
    /// <summary>Danh sách học kỳ, mới nhất trước.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SemesterDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List() =>
        Ok(await db.Semesters.AsNoTracking().OrderByDescending(s => s.StartDate)
            .Select(s => new SemesterDto(s.Id, s.Name, s.StartDate, s.EndDate, s.IsCurrent)).ToListAsync());

    /// <summary>Học kỳ hiện tại (IsCurrent = true). Chưa đặt học kỳ nào thì trả 404.</summary>
    [HttpGet("current")]
    [ProducesResponseType<SemesterDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Current()
    {
        var semester = await db.Semesters.AsNoTracking().FirstOrDefaultAsync(s => s.IsCurrent);
        return semester is null ? NotFound() : Ok(ToDto(semester));
    }

    /// <summary>Thêm học kỳ. Ngày kết thúc phải sau ngày bắt đầu. Đặt IsCurrent = true sẽ tự bỏ cờ của học kỳ khác.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<SemesterDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(UpsertSemesterRequest request)
    {
        if (request.EndDate <= request.StartDate)
            return InvalidField("EndDate", "Ngày kết thúc phải sau ngày bắt đầu");

        var semester = new Semester { Name = request.Name.Trim(), StartDate = request.StartDate, EndDate = request.EndDate, IsCurrent = request.IsCurrent };
        await SaveAsync(semester, isNew: true);
        return Created($"/api/semesters/{semester.Id}", ToDto(semester));
    }

    /// <summary>Sửa học kỳ.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<SemesterDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, UpsertSemesterRequest request)
    {
        var semester = await db.Semesters.FirstOrDefaultAsync(s => s.Id == id);
        if (semester is null)
            return NotFound();
        if (request.EndDate <= request.StartDate)
            return InvalidField("EndDate", "Ngày kết thúc phải sau ngày bắt đầu");

        semester.Name = request.Name.Trim();
        semester.StartDate = request.StartDate;
        semester.EndDate = request.EndDate;
        semester.IsCurrent = request.IsCurrent;
        await SaveAsync(semester, isNew: false);
        return Ok(ToDto(semester));
    }

    /// <summary>Xóa học kỳ. Học kỳ đã mở lớp thì trả 409.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var semester = await db.Semesters.FirstOrDefaultAsync(s => s.Id == id);
        if (semester is null)
            return NotFound();
        if (await db.CourseSections.AnyAsync(s => s.SemesterId == id))
            return ConflictProblem("Học kỳ đã mở lớp, không xóa được");

        db.Semesters.Remove(semester);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // Chỉ một học kỳ được IsCurrent = true (có unique index): bỏ cờ các học kỳ khác trước rồi mới lưu, trong một transaction
    private Task<bool> SaveAsync(Semester semester, bool isNew) =>
        db.InTransactionAsync(async () =>
        {
            if (semester.IsCurrent)
            {
                await db.Semesters.Where(s => s.IsCurrent && s.Id != semester.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsCurrent, false));
            }
            if (isNew)
            {
                db.Semesters.Add(semester);
            }
            await db.SaveChangesAsync();
            return true;
        });

    private static SemesterDto ToDto(Semester s) => new(s.Id, s.Name, s.StartDate, s.EndDate, s.IsCurrent);
}

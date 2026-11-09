using DACN.Api.Auth;
using DACN.Api.Data;
using DACN.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Controllers;

/// <summary>Danh sách sinh viên và giảng viên, dùng cho các ô chọn khi tạo lớp học phần hoặc thêm sinh viên vào lớp. Chỉ Admin.</summary>
[ApiController]
[Route("api")]
[Authorize(Roles = Roles.Admin)]
public class PeopleController(AppDbContext db) : ApiControllerBase
{
    /// <summary>Danh sách sinh viên đang hoạt động, tìm theo mã hoặc họ tên, có phân trang.</summary>
    [HttpGet("students")]
    [ProducesResponseType<PagedResult<StudentItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Students([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Students.AsNoTracking().Where(s => s.User.IsActive);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(s => s.StudentCode.Contains(term) || s.User.FullName.Contains(term));
        }

        var total = await query.CountAsync();
        var items = await query.OrderBy(s => s.StudentCode).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new StudentItem(s.Id, s.StudentCode, s.User.FullName, s.ClassName, s.Department.Name))
            .ToListAsync();
        return Ok(new PagedResult<StudentItem>(items, page, pageSize, total));
    }

    /// <summary>Danh sách giảng viên đang hoạt động.</summary>
    [HttpGet("lecturers")]
    [ProducesResponseType<IReadOnlyList<LecturerItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Lecturers() =>
        Ok(await db.Lecturers.AsNoTracking().Where(l => l.User.IsActive).OrderBy(l => l.LecturerCode)
            .Select(l => new LecturerItem(l.Id, l.LecturerCode, l.User.FullName, l.Department.Name))
            .ToListAsync());
}

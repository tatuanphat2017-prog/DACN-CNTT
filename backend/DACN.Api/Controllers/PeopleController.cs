using DACN.Api.Auth;
using DACN.Api.Dtos;
using DACN.Api.Mock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DACN.Api.Controllers;

/// <summary>Danh sách sinh viên và giảng viên, dùng cho các ô chọn khi tạo lớp học phần hoặc thêm sinh viên vào lớp. Chỉ Admin.</summary>
[ApiController]
[Route("api")]
[Authorize(Roles = Roles.Admin)]
[Mock]
public class PeopleController : ControllerBase
{
    /// <summary>Danh sách sinh viên, tìm theo mã hoặc họ tên, có phân trang.</summary>
    [HttpGet("students")]
    [ProducesResponseType<PagedResult<StudentItem>>(StatusCodes.Status200OK)]
    public IActionResult Students([FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var items = MockData.StudentItems.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            items = items.Where(s => s.StudentCode.Contains(q, StringComparison.OrdinalIgnoreCase)
                                     || s.FullName.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        return Ok(MockData.Page(items, page, pageSize));
    }

    /// <summary>Danh sách giảng viên.</summary>
    [HttpGet("lecturers")]
    [ProducesResponseType<IReadOnlyList<LecturerItem>>(StatusCodes.Status200OK)]
    public IActionResult Lecturers() => Ok(MockData.Lecturers);
}

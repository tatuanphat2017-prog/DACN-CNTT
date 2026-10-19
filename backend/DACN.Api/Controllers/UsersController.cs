using DACN.Api.Auth;
using DACN.Api.Dtos;
using DACN.Api.Entities;
using DACN.Api.Mock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DACN.Api.Controllers;

/// <summary>Quản lý tài khoản người dùng. Chỉ Admin.</summary>
[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Admin)]
[Mock]
public class UsersController : ControllerBase
{
    /// <summary>Danh sách người dùng, lọc theo role và từ khóa (username hoặc họ tên), có phân trang.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<UserListItem>>(StatusCodes.Status200OK)]
    public IActionResult List([FromQuery] UserRole? role, [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var items = MockData.Users.AsEnumerable();
        if (role is not null) items = items.Where(u => u.Role == role);
        if (!string.IsNullOrWhiteSpace(q))
        {
            items = items.Where(u => u.Username.Contains(q, StringComparison.OrdinalIgnoreCase)
                                     || u.FullName.Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        return Ok(MockData.Page(items, page, pageSize));
    }

    /// <summary>Chi tiết một người dùng, kèm hồ sơ sinh viên hoặc giảng viên.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<UserDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Get(int id) => MockData.UserDetail(id) is { } user ? Ok(user) : NotFound();

    /// <summary>Tạo tài khoản. Role = Student phải có Student, Role = Lecturer phải có Lecturer. Username trùng trả 409.</summary>
    [HttpPost]
    [ProducesResponseType<UserDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Create(CreateUserRequest request)
    {
        if (request.Role == UserRole.Student && request.Student is null)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["Student"] = ["Role Student phải có hồ sơ Student"] }));
        if (request.Role == UserRole.Lecturer && request.Lecturer is null)
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["Lecturer"] = ["Role Lecturer phải có hồ sơ Lecturer"] }));
        if (MockData.Users.Any(u => u.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase)))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Username đã tồn tại");

        var created = new UserDetail(100, request.Username, request.FullName, request.Email, request.Role, true,
            DateTime.UtcNow, request.Student, request.Lecturer);
        return Created("/api/users/100", created);
    }

    /// <summary>Sửa thông tin. Muốn khóa tài khoản thì đặt IsActive = false (không xóa dòng dữ liệu để giữ lịch sử).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<UserDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Update(int id, UpdateUserRequest request)
    {
        if (MockData.UserDetail(id) is not { } user) return NotFound();
        return Ok(user with { FullName = request.FullName, Email = request.Email, IsActive = request.IsActive });
    }

    /// <summary>Đặt lại mật khẩu cho người dùng.</summary>
    [HttpPost("{id:int}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult ResetPassword(int id, ResetPasswordRequest request) =>
        MockData.UserDetail(id) is null ? NotFound() : NoContent();
}

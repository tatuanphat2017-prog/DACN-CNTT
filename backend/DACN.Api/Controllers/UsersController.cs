using DACN.Api.Auth;
using DACN.Api.Data;
using DACN.Api.Dtos;
using DACN.Api.Entities;
using DACN.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Controllers;

/// <summary>Quản lý tài khoản người dùng. Chỉ Admin.</summary>
[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Admin)]
public class UsersController(AppDbContext db, IPasswordHasher<User> hasher, ICurrentUser current) : ApiControllerBase
{
    /// <summary>Danh sách người dùng, lọc theo role và từ khóa (username, họ tên hoặc email), có phân trang.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<UserListItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] UserRole? role, [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Users.AsNoTracking();
        if (role is not null)
        {
            query = query.Where(u => u.Role == role);
        }
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(u => u.Username.Contains(term) || u.FullName.Contains(term) || (u.Email != null && u.Email.Contains(term)));
        }

        var total = await query.CountAsync();
        var items = await query.OrderBy(u => u.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new UserListItem(u.Id, u.Username, u.FullName, u.Email, u.Role, u.IsActive, u.CreatedAt))
            .ToListAsync();
        return Ok(new PagedResult<UserListItem>(items, page, pageSize, total));
    }

    /// <summary>Chi tiết một người dùng, kèm hồ sơ sinh viên hoặc giảng viên.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<UserDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id)
    {
        var user = await db.Users.AsNoTracking().Include(u => u.Student).Include(u => u.Lecturer).FirstOrDefaultAsync(u => u.Id == id);
        return user is null ? NotFound() : Ok(ToDetail(user));
    }

    /// <summary>Tạo tài khoản. Role = Student phải có Student, Role = Lecturer phải có Lecturer. Username, email, mã sinh viên, mã giảng viên trùng trả 409.</summary>
    [HttpPost]
    [ProducesResponseType<UserDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        var username = request.Username.Trim();
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

        if (request.Role == UserRole.Student && request.Student is null)
            return InvalidField("Student", "Role Student phải có hồ sơ Student");
        if (request.Role == UserRole.Lecturer && request.Lecturer is null)
            return InvalidField("Lecturer", "Role Lecturer phải có hồ sơ Lecturer");

        var departmentId = request.Role switch
        {
            UserRole.Student => request.Student!.DepartmentId,
            UserRole.Lecturer => request.Lecturer!.DepartmentId,
            _ => (int?)null,
        };
        if (departmentId is not null && !await db.Departments.AnyAsync(d => d.Id == departmentId))
            return InvalidField("DepartmentId", "Khoa không tồn tại");

        if (await db.Users.AnyAsync(u => u.Username == username))
            return ConflictProblem("Username đã tồn tại");
        if (email is not null && await db.Users.AnyAsync(u => u.Email == email))
            return ConflictProblem("Email đã được dùng");

        var user = new User { Username = username, FullName = request.FullName.Trim(), Email = email, Role = request.Role };
        user.PasswordHash = hasher.HashPassword(user, request.Password);

        if (request.Role == UserRole.Student)
        {
            var profile = request.Student!;
            if (await db.Students.AnyAsync(s => s.StudentCode == profile.StudentCode))
                return ConflictProblem("Mã sinh viên đã tồn tại");
            user.Student = new Student
            {
                StudentCode = profile.StudentCode.Trim(), DepartmentId = profile.DepartmentId,
                ClassName = profile.ClassName.Trim(), EnrollmentYear = profile.EnrollmentYear,
            };
        }
        else if (request.Role == UserRole.Lecturer)
        {
            var profile = request.Lecturer!;
            if (await db.Lecturers.AnyAsync(l => l.LecturerCode == profile.LecturerCode))
                return ConflictProblem("Mã giảng viên đã tồn tại");
            user.Lecturer = new Lecturer { LecturerCode = profile.LecturerCode.Trim(), DepartmentId = profile.DepartmentId };
        }

        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ConflictProblem("Dữ liệu bị trùng (username, email hoặc mã), vui lòng thử lại");
        }
        return Created($"/api/users/{user.Id}", ToDetail(user));
    }

    /// <summary>
    /// Sửa thông tin. Muốn khóa tài khoản thì đặt IsActive = false (không xóa dòng dữ liệu để giữ lịch sử).
    /// Không tự khóa chính mình và không khóa admin cuối cùng. Username không đổi được.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<UserDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, UpdateUserRequest request)
    {
        var user = await db.Users.Include(u => u.Student).Include(u => u.Lecturer).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            return NotFound();

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (email is not null && await db.Users.AnyAsync(u => u.Email == email && u.Id != id))
            return ConflictProblem("Email đã được dùng");

        if (!request.IsActive && user.IsActive)
        {
            if (id == current.UserId)
                return InvalidField("IsActive", "Không thể tự khóa tài khoản của chính mình");
            if (user.Role == UserRole.Admin && !await db.Users.AnyAsync(u => u.Role == UserRole.Admin && u.IsActive && u.Id != id))
                return InvalidField("IsActive", "Không thể khóa admin cuối cùng của hệ thống");
        }

        if (user.Student is not null && request.Student is { } student)
        {
            if (!await db.Departments.AnyAsync(d => d.Id == student.DepartmentId))
                return InvalidField("DepartmentId", "Khoa không tồn tại");
            if (await db.Students.AnyAsync(s => s.StudentCode == student.StudentCode && s.Id != user.Student.Id))
                return ConflictProblem("Mã sinh viên đã tồn tại");
            user.Student.StudentCode = student.StudentCode.Trim();
            user.Student.DepartmentId = student.DepartmentId;
            user.Student.ClassName = student.ClassName.Trim();
            user.Student.EnrollmentYear = student.EnrollmentYear;
        }
        if (user.Lecturer is not null && request.Lecturer is { } lecturer)
        {
            if (!await db.Departments.AnyAsync(d => d.Id == lecturer.DepartmentId))
                return InvalidField("DepartmentId", "Khoa không tồn tại");
            if (await db.Lecturers.AnyAsync(l => l.LecturerCode == lecturer.LecturerCode && l.Id != user.Lecturer.Id))
                return ConflictProblem("Mã giảng viên đã tồn tại");
            user.Lecturer.LecturerCode = lecturer.LecturerCode.Trim();
            user.Lecturer.DepartmentId = lecturer.DepartmentId;
        }

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.IsActive = request.IsActive;
        await db.SaveChangesAsync();
        return Ok(ToDetail(user));
    }

    /// <summary>Đặt lại mật khẩu cho người dùng (Admin đặt mật khẩu mới thay họ).</summary>
    [HttpPost("{id:int}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            return NotFound();
        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static UserDetail ToDetail(User u) =>
        new(u.Id, u.Username, u.FullName, u.Email, u.Role, u.IsActive, u.CreatedAt,
            u.Student is null ? null : new StudentProfile(u.Student.StudentCode, u.Student.DepartmentId, u.Student.ClassName, u.Student.EnrollmentYear),
            u.Lecturer is null ? null : new LecturerProfile(u.Lecturer.LecturerCode, u.Lecturer.DepartmentId));
}

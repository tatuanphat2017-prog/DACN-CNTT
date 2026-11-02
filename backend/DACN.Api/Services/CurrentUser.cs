using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DACN.Api.Auth;
using DACN.Api.Data;
using DACN.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Services;

/// <summary>Người dùng đang gọi API (đọc từ JWT), kèm id giảng viên / sinh viên tương ứng.</summary>
public interface ICurrentUser
{
    int UserId { get; }
    UserRole Role { get; }
    Task<int?> GetLecturerIdAsync();
    Task<int?> GetStudentIdAsync();
}

public class CurrentUser(IHttpContextAccessor accessor, AppDbContext db) : ICurrentUser
{
    private int? _lecturerId;
    private int? _studentId;
    private bool _lecturerLoaded;
    private bool _studentLoaded;

    private ClaimsPrincipal Principal =>
        accessor.HttpContext?.User ?? throw new InvalidOperationException("Không có HttpContext");

    public int UserId => int.Parse(Principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    public UserRole Role => Enum.Parse<UserRole>(Principal.FindFirstValue(TokenService.RoleClaim)!);

    public async Task<int?> GetLecturerIdAsync()
    {
        if (!_lecturerLoaded)
        {
            var userId = UserId;
            _lecturerId = await db.Lecturers.Where(l => l.UserId == userId).Select(l => (int?)l.Id).FirstOrDefaultAsync();
            _lecturerLoaded = true;
        }
        return _lecturerId;
    }

    public async Task<int?> GetStudentIdAsync()
    {
        if (!_studentLoaded)
        {
            var userId = UserId;
            _studentId = await db.Students.Where(s => s.UserId == userId).Select(s => (int?)s.Id).FirstOrDefaultAsync();
            _studentLoaded = true;
        }
        return _studentId;
    }
}

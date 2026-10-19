using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DACN.Api.Auth;
using DACN.Api.Data;
using DACN.Api.Dtos;
using DACN.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, TokenService tokenService, IPasswordHasher<User> passwordHasher) : ControllerBase
{
    // Hash giả để vẫn tốn thời gian kiểm tra khi username không tồn tại (tránh dò username qua thời gian phản hồi)
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(new User(), "dummy-password");

    /// <summary>Đăng nhập bằng username (MSSV / mã GV / admin) và mật khẩu, trả về JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Username == request.Username);

        var result = passwordHasher.VerifyHashedPassword(user ?? new User(), user?.PasswordHash ?? DummyHash, request.Password);
        if (user is null || !user.IsActive || result == PasswordVerificationResult.Failed)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sai tên đăng nhập hoặc mật khẩu");
        }

        // Thuật toán băm được nâng cấp thì lưu lại hash mới
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync();
        }

        var (token, expiresAt) = tokenService.CreateToken(user);
        return Ok(new LoginResponse(token, expiresAt, ToUserInfo(user)));
    }

    /// <summary>Thông tin người đang đăng nhập (cần gửi kèm token).</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserInfo>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me()
    {
        var userId = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var user = await db.Users.FindAsync(userId);
        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }
        return Ok(ToUserInfo(user));
    }

    private static UserInfo ToUserInfo(User user) =>
        new(user.Id, user.Username, user.FullName, user.Email, user.Role.ToString());
}

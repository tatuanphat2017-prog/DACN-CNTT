using System.ComponentModel.DataAnnotations;

namespace DACN.Api.Dtos;

public record LoginRequest(
    [Required, MaxLength(50)] string Username,
    [Required, MaxLength(100)] string Password);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8), MaxLength(100)] string NewPassword);

public record UserInfo(int Id, string Username, string FullName, string? Email, string Role);

public record LoginResponse(string AccessToken, DateTime ExpiresAt, UserInfo User);

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DACN.Api.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DACN.Api.Auth;

public class TokenService(IOptions<JwtOptions> options)
{
    public const string RoleClaim = "role";

    private readonly JwtOptions _jwt = options.Value;

    public (string Token, DateTime ExpiresAt) CreateToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwt.ExpiresMinutes);
        // Dùng tên claim ngắn chuẩn JWT để frontend đọc token dễ dàng
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Name, user.Username),
            new Claim(RoleClaim, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims,
            expires: expiresAt, signingCredentials: credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}

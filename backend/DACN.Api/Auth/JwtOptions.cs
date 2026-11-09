namespace DACN.Api.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "DACN.Api";
    public string Audience { get; set; } = "DACN.Client";

    // Khóa ký token, tối thiểu 32 ký tự. Không ghi vào appsettings.json:
    // local dùng user-secrets, Docker/server dùng biến môi trường Jwt__Key.
    public string Key { get; set; } = string.Empty;

    public int ExpiresMinutes { get; set; } = 480;
}

using DACN.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Data;

public static class DbSeeder
{
    // Tạo tài khoản admin đầu tiên nếu hệ thống chưa có admin nào.
    // Mật khẩu lấy từ cấu hình Seed:AdminPassword (user-secrets / biến môi trường), không ghi trong code.
    public static async Task SeedAdminAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var config = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILogger<AppDbContext>>();

        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin))
        {
            return;
        }

        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Chưa có tài khoản admin và chưa cấu hình Seed:AdminPassword, bỏ qua bước tạo admin.");
            return;
        }

        var admin = new User
        {
            Username = config["Seed:AdminUsername"] ?? "admin",
            FullName = "Quản trị viên",
            Role = UserRole.Admin,
        };
        admin.PasswordHash = services.GetRequiredService<IPasswordHasher<User>>().HashPassword(admin, password);
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        logger.LogInformation("Đã tạo tài khoản admin mặc định: {Username}", admin.Username);
    }
}

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

    // Tạo tài khoản demo (giảng viên GV001–GV002, sinh viên SV001–SV008) để Frontend đăng nhập thử từng role.
    // Chỉ chạy khi có cấu hình Seed:DemoPassword; không đặt biến này trên server thật nếu không muốn có tài khoản demo.
    // Dùng tên lấy từ MockData cho khớp dữ liệu mẫu; sẽ thay bằng seed dữ liệu demo đầy đủ ở tuần 7.
    public static async Task SeedDemoUsersAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var password = services.GetRequiredService<IConfiguration>()["Seed:DemoPassword"];
        var logger = services.GetRequiredService<ILogger<AppDbContext>>();

        if (string.IsNullOrWhiteSpace(password) || await db.Users.AnyAsync(u => u.Username == "GV001"))
        {
            return;
        }

        var hasher = services.GetRequiredService<IPasswordHasher<User>>();
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Code == "CNTT")
                         ?? db.Departments.Add(new Department { Code = "CNTT", Name = "Công nghệ thông tin" }).Entity;

        foreach (var item in Mock.MockData.Users.Where(u => u.Role != UserRole.Admin))
        {
            var detail = Mock.MockData.UserDetail(item.Id)!;
            var user = new User { Username = item.Username, FullName = item.FullName, Email = item.Email, Role = item.Role };
            user.PasswordHash = hasher.HashPassword(user, password);

            if (detail.Lecturer is { } lecturer)
            {
                db.Lecturers.Add(new Lecturer { User = user, LecturerCode = lecturer.LecturerCode, Department = department });
            }
            else if (detail.Student is { } student)
            {
                db.Students.Add(new Student
                {
                    User = user, StudentCode = student.StudentCode, Department = department,
                    ClassName = student.ClassName, EnrollmentYear = student.EnrollmentYear,
                });
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Đã tạo tài khoản demo: giảng viên GV001–GV002, sinh viên SV001–SV008");
    }
}

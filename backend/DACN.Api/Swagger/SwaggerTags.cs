using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DACN.Api.Swagger;

/// <summary>Tên nhóm tiếng Việt trên Swagger cho từng controller, theo thứ tự hiển thị.</summary>
public static class SwaggerTags
{
    public static readonly (string Controller, string Tag)[] Order =
    [
        ("Auth", "Đăng nhập"),
        ("Users", "Quản trị: tài khoản"),
        ("People", "Quản trị: sinh viên, giảng viên"),
        ("Departments", "Danh mục: khoa"),
        ("Courses", "Danh mục: môn học"),
        ("Semesters", "Danh mục: học kỳ"),
        ("Sections", "Lớp học phần"),
        ("Import", "Nhập Excel"),
        ("Attendance", "Điểm danh"),
        ("Grades", "Điểm"),
        ("Risk", "Rủi ro (ML)"),
        ("Warnings", "Cảnh báo"),
        ("StudentPortal", "Cổng sinh viên"),
        ("Ml", "Mô hình ML"),
        ("Health", "Kiểm tra hệ thống"),
    ];

    public static string For(string? controller) =>
        Order.FirstOrDefault(x => x.Controller == controller).Tag ?? controller ?? "Khác";
}

/// <summary>Sắp xếp các nhóm trên Swagger theo luồng sử dụng thay vì theo thứ tự chữ cái.</summary>
public class TagOrderDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context) =>
        swaggerDoc.Tags = new HashSet<OpenApiTag>(SwaggerTags.Order.Select(x => new OpenApiTag { Name = x.Tag }));
}

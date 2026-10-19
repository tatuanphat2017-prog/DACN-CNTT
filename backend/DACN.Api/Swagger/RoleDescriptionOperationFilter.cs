using DACN.Api.Mock;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DACN.Api.Swagger;

/// <summary>
/// Tự ghi vào mô tả của từng API: ai được gọi (theo [Authorize]), có phải dữ liệu MOCK không,
/// và bổ sung mã lỗi 401/403 tương ứng. Frontend đọc trực tiếp trên Swagger.
/// </summary>
public class RoleDescriptionOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var method = context.MethodInfo;
        var controller = method.DeclaringType!;

        var methodAuth = method.GetCustomAttributes(true).OfType<AuthorizeAttribute>().ToList();
        var auth = methodAuth.Count > 0 ? methodAuth : controller.GetCustomAttributes(true).OfType<AuthorizeAttribute>().ToList();
        var anonymous = method.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any();

        string access;
        string? roles = null;
        if (anonymous || auth.Count == 0)
        {
            access = "Công khai, không cần đăng nhập";
        }
        else
        {
            roles = auth.Select(a => a.Roles).LastOrDefault(r => !string.IsNullOrEmpty(r));
            access = roles is null ? "Mọi người dùng đã đăng nhập" : "Role " + roles.Replace(",", " hoặc ");
        }

        var isMock = controller.IsDefined(typeof(MockAttribute), true);
        var note = $"**Quyền truy cập:** {access}" + (isMock ? "\n\n**MOCK:** đang trả dữ liệu mẫu cố định, chưa có logic thật." : "");
        operation.Description = string.IsNullOrEmpty(operation.Description) ? note : operation.Description + "\n\n" + note;

        if (!anonymous && auth.Count > 0)
        {
            // Ổ khóa 🔒 trên Swagger chỉ hiện ở API cần đăng nhập
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [],
            });

            operation.Responses ??= new OpenApiResponses();
            operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Chưa đăng nhập hoặc token hết hạn" });
            if (roles is not null)
            {
                operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Đã đăng nhập nhưng không đủ quyền" });
            }
        }
    }
}

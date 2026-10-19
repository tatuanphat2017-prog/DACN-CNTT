using Microsoft.AspNetCore.Mvc.Filters;

namespace DACN.Api.Mock;

/// <summary>
/// Đánh dấu controller đang trả dữ liệu mẫu (chưa có logic thật). Response có thêm header X-Mock-Data: true,
/// Swagger ghi chú "MOCK". Khi viết logic thật cho controller nào thì gỡ attribute này khỏi controller đó.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class MockAttribute : ResultFilterAttribute
{
    public override void OnResultExecuting(ResultExecutingContext context) =>
        context.HttpContext.Response.Headers["X-Mock-Data"] = "true";
}

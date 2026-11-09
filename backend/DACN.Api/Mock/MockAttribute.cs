using Microsoft.AspNetCore.Mvc.Filters;

namespace DACN.Api.Mock;

/// <summary>
/// Đánh dấu controller (hoặc một API riêng lẻ) đang trả dữ liệu mẫu (chưa có logic thật). Response có thêm header X-Mock-Data: true,
/// Swagger ghi chú "MOCK". Khi viết logic thật cho controller/API nào thì gỡ attribute này khỏi đó.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class MockAttribute : ResultFilterAttribute
{
    public override void OnResultExecuting(ResultExecutingContext context) =>
        context.HttpContext.Response.Headers["X-Mock-Data"] = "true";
}

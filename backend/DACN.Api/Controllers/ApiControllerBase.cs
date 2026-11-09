using DACN.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DACN.Api.Controllers;

/// <summary>Lớp cha của các controller, gom các hàm trả lỗi dùng chung.</summary>
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>409: trùng dữ liệu hoặc còn dữ liệu liên quan.</summary>
    protected ObjectResult ConflictProblem(string title) =>
        Problem(statusCode: StatusCodes.Status409Conflict, title: title);

    /// <summary>400 với lỗi gắn vào một trường cụ thể.</summary>
    protected ActionResult InvalidField(string field, string message) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [field] = [message] }));

    /// <summary>
    /// Chạy một thao tác và chuyển lỗi nghiệp vụ thành ProblemDetails: ServiceException giữ mã của nó,
    /// MlException 503 nghĩa là ML service không khả dụng, mã 4xx là ML service từ chối yêu cầu.
    /// </summary>
    protected async Task<IActionResult> RunAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ServiceException ex)
        {
            return Problem(statusCode: ex.Status, title: ex.Message);
        }
        catch (MlException ex)
        {
            return Problem(statusCode: ex.Status, title: ex.Message);
        }
    }

    /// <summary>Chuyển kết quả kiểm tra quyền lớp học phần thành 404/403. Trả null nếu được phép.</summary>
    protected IActionResult? Reject(SectionAccess access) => access.Status switch
    {
        AccessStatus.NotFound => NotFound(),
        AccessStatus.Forbidden => Forbid(),
        _ => null,
    };
}

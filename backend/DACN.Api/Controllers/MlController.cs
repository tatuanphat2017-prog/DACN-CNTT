using DACN.Api.Auth;
using DACN.Api.Data;
using DACN.Api.Dtos;
using DACN.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DACN.Api.Controllers;

/// <summary>Quản lý mô hình Machine Learning. Chỉ Admin.</summary>
[ApiController]
[Route("api/ml")]
[Authorize(Roles = Roles.Admin)]
public class MlController(AppDbContext db, MlModelService models, IMlClient ml, RetrainJob retrain) : ApiControllerBase
{
    /// <summary>
    /// Danh sách các phiên bản mô hình và độ chính xác (Accuracy, Precision, Recall, F1, ROC-AUC), mới nhất trước.
    /// Tự đồng bộ từ ML service; nếu ML service không khả dụng thì trả dữ liệu đã lưu trong database.
    /// </summary>
    [HttpGet("models")]
    [ProducesResponseType<IReadOnlyList<ModelVersionItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Models()
    {
        try
        {
            await models.SyncAsync();
        }
        catch (MlException)
        {
            // ML service tắt: vẫn xem được các phiên bản đã đồng bộ trước đó
        }
        return Ok(await models.ListAsync());
    }

    /// <summary>
    /// Yêu cầu huấn luyện lại mô hình bằng dữ liệu thật trong database. Chạy nền, trả 202 ngay;
    /// theo dõi ở GET /api/ml/retrain/status, phiên bản mới xuất hiện trong GET /api/ml/models.
    /// Mô hình mới được dùng ngay nếu ROC-AUC không kém mô hình hiện tại quá 0.01. Đang huấn luyện thì trả 409.
    /// </summary>
    [HttpPost("retrain")]
    [ProducesResponseType<RetrainResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Retrain() =>
        retrain.TryStart()
            ? Accepted(new RetrainResponse("Started", "Đã bắt đầu huấn luyện lại mô hình"))
            : ConflictProblem("Đang có một lần huấn luyện khác đang chạy");

    /// <summary>Trạng thái lần huấn luyện lại gần nhất (Idle, Running, Succeeded, Failed) kèm thông báo kết quả.</summary>
    [HttpGet("retrain/status")]
    [ProducesResponseType<RetrainStatusDto>(StatusCodes.Status200OK)]
    public IActionResult RetrainStatus() => Ok(retrain.Status);

    /// <summary>Chuyển sang dùng một phiên bản mô hình đã lưu. Trả 404 nếu không có phiên bản đó, 503 nếu ML service không khả dụng.</summary>
    [HttpPost("models/{id:int}/activate")]
    [ProducesResponseType<ModelVersionItem>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> Activate(int id) =>
        RunAsync(async () =>
        {
            var version = await db.ModelVersions.Where(m => m.Id == id).Select(m => m.Version).FirstOrDefaultAsync();
            if (version is null)
                return NotFound();

            await ml.ActivateAsync(version);
            await models.SetActiveAsync(version);
            return Ok((await models.ListAsync()).First(m => m.Id == id));
        });
}

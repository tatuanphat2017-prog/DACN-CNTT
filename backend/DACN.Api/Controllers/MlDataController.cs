using DACN.Api.Auth;
using DACN.Api.Dtos;
using DACN.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DACN.Api.Controllers;

/// <summary>Dữ liệu cho Machine Learning. Chỉ Admin.</summary>
[ApiController]
[Route("api/ml")]
[Authorize(Roles = Roles.Admin)]
public class MlDataController(MlFeatureService features) : ApiControllerBase
{
    /// <summary>
    /// Dữ liệu huấn luyện lấy từ các đăng ký đã có kết quả cuối kỳ: đặc trưng chụp ở giữa kỳ và nhãn Failed.
    /// Không chứa thông tin định danh sinh viên (chỉ EnrollmentId). Script ml-service/scripts/export_training_data.py dùng API này.
    /// </summary>
    [HttpGet("training-data")]
    [ProducesResponseType<IReadOnlyList<TrainingRow>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> TrainingData() => Ok(await features.TrainingRowsAsync());
}

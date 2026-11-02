using DACN.Api.Data;
using DACN.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DACN.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController(AppDbContext db, IMlClient ml) : ControllerBase
{
    /// <summary>Kiểm tra backend, database và ML service có kết nối được không.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var database = await db.Database.CanConnectAsync();
        var mlService = await ml.IsHealthyAsync();
        return Ok(new { backend = true, database, mlService });
    }
}

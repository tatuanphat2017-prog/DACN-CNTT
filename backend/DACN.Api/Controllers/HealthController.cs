using DACN.Api.Data;
using Microsoft.AspNetCore.Mvc;

namespace DACN.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController(AppDbContext db, IHttpClientFactory httpClientFactory) : ControllerBase
{
    // Kiểm tra backend, database và ML service có kết nối được không
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var database = await db.Database.CanConnectAsync();

        bool mlService;
        try
        {
            var response = await httpClientFactory.CreateClient("ml").GetAsync("/health");
            mlService = response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            mlService = false;
        }

        return Ok(new { backend = true, database, mlService });
    }
}

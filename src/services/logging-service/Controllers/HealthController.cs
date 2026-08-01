using LoggingService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoggingService.Controllers;

/// <summary>
/// 健康检查 — 服务存活 + 数据库连通性。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class HealthController(LoggingDbContext db, ILogger<HealthController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> Check(CancellationToken ct)
    {
        var databaseOk = false;
        try
        {
            databaseOk = await db.Database.CanConnectAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "健康检查：数据库连接失败");
        }

        return databaseOk
            ? Ok(new { status = "healthy", database = "ok", timestamp = DateTime.UtcNow })
            : StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { status = "unhealthy", database = "unreachable", timestamp = DateTime.UtcNow });
    }
}

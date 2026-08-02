using Microsoft.AspNetCore.Mvc;
using ProductService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ProductService.Controllers;

/// <summary>
/// 健康检查 — 服务存活 + 数据库连通性。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class HealthController(ProductDbContext db, ILogger<HealthController> logger) : ControllerBase
{
    /// <summary>健康检查 — 服务存活 + 数据库连通性</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200（数据库正常）或 503（数据库不可达）</returns>
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

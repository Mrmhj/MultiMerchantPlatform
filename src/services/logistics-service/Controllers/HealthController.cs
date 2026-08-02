using Microsoft.AspNetCore.Mvc;

namespace LogisticsService.Controllers;

/// <summary>
/// 健康检查接口。
/// </summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    /// <summary>健康检查（数据库连通性）</summary>
    /// <param name="db">数据库上下文</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>健康状态</returns>
    /// <response code="200">服务与数据库正常</response>
    /// <response code="503">数据库不可用</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult> Check(
        [FromServices] Infrastructure.Persistence.LogisticsDbContext db, CancellationToken ct)
    {
        var dbOk = await db.Database.CanConnectAsync(ct);
        return dbOk
            ? Ok(new { status = "healthy", database = "ok" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "unhealthy", database = "down" });
    }
}

using LoggingService.Application;
using Microsoft.AspNetCore.Mvc;

namespace LoggingService.Controllers;

/// <summary>
/// 日志统计 API — 级别分布 / Top 服务 / 时间趋势 / 错误率。
/// </summary>
[ApiController]
[Route("api/log-stats")]
[Produces("application/json")]
public sealed class LogStatsController(LogStatsService statsService) : ControllerBase
{
    /// <summary>按级别分布</summary>
    [HttpGet("level-distribution")]
    public async Task<ActionResult<IReadOnlyList<LevelDistribution>>> LevelDistribution(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await statsService.GetLevelDistributionAsync(from, to, ct));

    /// <summary>日志量 Top N 服务</summary>
    [HttpGet("top-services")]
    public async Task<ActionResult<IReadOnlyList<TopService>>> TopServices(
        [FromQuery] int top = 10, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, CancellationToken ct = default)
        => Ok(await statsService.GetTopServicesAsync(top, from, to, ct));

    /// <summary>时间趋势（granularity: hour | day，默认最近 24 小时按小时）</summary>
    [HttpGet("trend")]
    public async Task<ActionResult<IReadOnlyList<TrendPoint>>> Trend(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string granularity = "hour", CancellationToken ct = default)
        => Ok(await statsService.GetTrendAsync(from, to, granularity, ct));

    /// <summary>错误率（Error + Critical 占比，%）</summary>
    [HttpGet("error-rate")]
    public async Task<ActionResult<double>> ErrorRate(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct = default)
        => Ok(await statsService.GetErrorRateAsync(from, to, ct));
}

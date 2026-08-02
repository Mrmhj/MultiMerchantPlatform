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
    /// <summary>按级别分布统计</summary>
    /// <param name="from">起始时间（可选）</param>
    /// <param name="to">结束时间（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 各日志级别的条数分布</returns>
    [HttpGet("level-distribution")]
    public async Task<ActionResult<IReadOnlyList<LevelDistribution>>> LevelDistribution(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await statsService.GetLevelDistributionAsync(from, to, ct));

    /// <summary>日志量 Top N 服务</summary>
    /// <param name="top">取前 N 个服务（默认 10）</param>
    /// <param name="from">起始时间（可选）</param>
    /// <param name="to">结束时间（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 日志量 Top N 服务列表</returns>
    [HttpGet("top-services")]
    public async Task<ActionResult<IReadOnlyList<TopService>>> TopServices(
        [FromQuery] int top = 10, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, CancellationToken ct = default)
        => Ok(await statsService.GetTopServicesAsync(top, from, to, ct));

    /// <summary>时间趋势（granularity: hour | day，默认最近 24 小时按小时）</summary>
    /// <param name="from">起始时间（可选）</param>
    /// <param name="to">结束时间（可选）</param>
    /// <param name="granularity">聚合粒度 hour|day（默认 hour）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 时间趋势点列表</returns>
    [HttpGet("trend")]
    public async Task<ActionResult<IReadOnlyList<TrendPoint>>> Trend(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string granularity = "hour", CancellationToken ct = default)
        => Ok(await statsService.GetTrendAsync(from, to, granularity, ct));

    /// <summary>错误率（Error + Critical 占比，%）</summary>
    /// <param name="from">起始时间（可选）</param>
    /// <param name="to">结束时间（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 错误率百分比</returns>
    [HttpGet("error-rate")]
    public async Task<ActionResult<double>> ErrorRate(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct = default)
        => Ok(await statsService.GetErrorRateAsync(from, to, ct));
}

using BuildingBlocks.Core.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PerformanceService.Application.Queries;
using PerformanceService.Application.Services;
using PerformanceService.DTOs;

namespace PerformanceService.Controllers;

/// <summary>
/// 监控指标接口（平台端）— 查询各微服务最新 / 历史指标快照、已监控服务列表、手动触发采集，需 admin 角色。
/// </summary>
[ApiController]
[Authorize(Roles = "admin")]
[Route("api/performance/metrics")]
[Produces("application/json")]
public sealed class MetricsController(IMediator mediator, MetricsCollector collector) : ControllerBase
{
    /// <summary>查询最新指标快照（每个服务最新一轮采样）</summary>
    /// <param name="service">服务名（可选，缺省返回全部服务）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 最新指标快照列表</returns>
    /// <response code="200">指标快照列表</response>
    [HttpGet("latest")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MetricsSnapshotResponse>>> Latest(
        [FromQuery] string? service, CancellationToken ct)
    {
        return Ok(await mediator.QueryAsync<MetricsLatestQuery, List<MetricsSnapshotResponse>>(
            new MetricsLatestQuery(service), ct));
    }

    /// <summary>查询指标历史（趋势数据）</summary>
    /// <param name="service">服务名（必填）</param>
    /// <param name="from">开始时间（可选）</param>
    /// <param name="to">结束时间（可选）</param>
    /// <param name="limit">返回条数上限（默认 500，上限 2000）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 指标历史（时间升序）</returns>
    /// <response code="200">指标历史</response>
    [HttpGet("history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MetricsSnapshotResponse>>> History(
        [FromQuery] string service, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int limit = 500, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(service))
            return BadRequest(new { error = "service 参数必填", code = "SERVICE_REQUIRED" });

        return Ok(await mediator.QueryAsync<MetricsHistoryQuery, List<MetricsSnapshotResponse>>(
            new MetricsHistoryQuery(service, from, to, limit), ct));
    }

    /// <summary>已监控服务列表</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 服务名列表</returns>
    /// <response code="200">服务名列表</response>
    [HttpGet("services")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> Services(CancellationToken ct)
    {
        return Ok(await mediator.QueryAsync<MonitoredServicesQuery, List<string>>(new MonitoredServicesQuery(), ct));
    }

    /// <summary>手动触发一轮指标采集（调试 / 演示用）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 采集完成</returns>
    /// <response code="200">采集完成</response>
    [HttpPost("collect")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> Collect(CancellationToken ct)
    {
        await collector.CollectOnceAsync(ct);
        return Ok(new { collected = true });
    }
}

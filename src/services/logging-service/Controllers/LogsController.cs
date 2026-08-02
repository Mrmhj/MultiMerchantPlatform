using BuildingBlocks.Core.Results;
using LoggingService.Application;
using LoggingService.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace LoggingService.Controllers;

/// <summary>
/// 日志 API — 批量上报 / 查询 / 详情（与 BuildingBlocks.Logging 客户端契约一致）。
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class LogsController(LogIngestService ingestService, LogQueryService queryService) : ControllerBase
{
    /// <summary>批量上报日志（客户端定时调用）</summary>
    /// <param name="entries">日志条目数组（非空）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 批量上报结果（含入库条数）</returns>
    [HttpPost("batch")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BatchIngestResult>> Batch([FromBody] IEnumerable<LogEntryDto> entries, CancellationToken ct)
    {
        var count = await ingestService.IngestAsync(entries, ct);
        return Created("", new BatchIngestResult { Ingested = count });
    }

    /// <summary>分页查询日志（支持服务/级别/关键字/时间范围过滤）</summary>
    /// <param name="query">查询条件（服务名 / 级别 / 关键字 / 时间范围 / 分页）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 分页日志列表</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<LogResponse>>> Query([FromQuery] LogQueryDto query, CancellationToken ct)
    {
        var result = await queryService.QueryAsync(query, ct);
        return Ok(result);
    }

    /// <summary>按 Id 查询日志详情</summary>
    /// <param name="id">日志 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 日志详情；404 — 日志不存在</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LogResponse>> GetById(Guid id, CancellationToken ct)
    {
        var log = await queryService.GetByIdAsync(id, ct);
        return log is null ? NotFound($"日志 {id} 不存在") : Ok(log);
    }

    /// <summary>批量上报结果</summary>
    public sealed record BatchIngestResult
    {
        /// <summary>成功入库的日志条数</summary>
        public int Ingested { get; init; }
    }
}

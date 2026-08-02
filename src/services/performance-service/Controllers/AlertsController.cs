using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PerformanceService.Application.Commands;
using PerformanceService.Application.Queries;
using PerformanceService.DTOs;

namespace PerformanceService.Controllers;

/// <summary>
/// 告警管理接口（平台端）— 告警列表查询与手动关闭，需 admin 角色。
/// </summary>
[ApiController]
[Authorize(Roles = "admin")]
[Route("api/performance/alerts")]
[Produces("application/json")]
public sealed class AlertsController(IMediator mediator) : ControllerBase
{
    /// <summary>告警列表（分页，可按状态 / 服务过滤）</summary>
    /// <param name="status">状态（Open/Resolved，可选）</param>
    /// <param name="service">服务名（可选）</param>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 告警分页列表</returns>
    /// <response code="200">告警列表</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AlertResponse>>> List(
        [FromQuery] string? status, [FromQuery] string? service,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await mediator.QueryAsync<AlertListQuery, PagedResult<AlertResponse>>(
            new AlertListQuery(status, service, page, pageSize), ct));
    }

    /// <summary>手动关闭告警</summary>
    /// <param name="id">告警 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 关闭后的告警；404 — 告警不存在</returns>
    /// <response code="200">已关闭</response>
    /// <response code="404">告警不存在</response>
    [HttpPut("{id:guid}/resolve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlertResponse>> Resolve(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<ResolveAlertCommand, AlertResponse>(
                new ResolveAlertCommand(id), ct));
        }
        catch (DomainException ex)
        {
            return NotFound(new { error = ex.Message, code = ex.ErrorCode });
        }
    }
}

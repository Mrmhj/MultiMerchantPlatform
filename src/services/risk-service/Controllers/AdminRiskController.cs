using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using RiskService.Application.Commands;
using RiskService.Application.Queries;
using RiskService.Domain.Enums;
using RiskService.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RiskService.Controllers;

/// <summary>
/// 风控管理接口（平台端）— 规则引擎配置 / 风险案例处置 / 黑名单 / 事件流水 / 概览，需 admin 角色。
/// </summary>
[ApiController]
[Authorize(Roles = "admin")]
[Route("api/risk")]
[Produces("application/json")]
public sealed class AdminRiskController(IMediator mediator) : ControllerBase
{
    /// <summary>风控概览（规则数/黑名单/待处置案例/今日事件与命中）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 概览数据</returns>
    /// <response code="200">概览数据</response>
    [HttpGet("overview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<RiskOverviewResponse>> Overview(CancellationToken ct)
        => Ok(await mediator.QueryAsync<RiskOverviewQuery, RiskOverviewResponse>(new RiskOverviewQuery(), ct));

    // ─────────────────── 规则引擎配置 ───────────────────

    /// <summary>规则列表（分页，可按场景/启用状态过滤）</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="scene">场景过滤（可选）</param>
    /// <param name="enabled">启用状态过滤（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 规则分页列表</returns>
    /// <response code="200">规则列表</response>
    [HttpGet("rules")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RiskRuleResponse>>> ListRules(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? scene = null, [FromQuery] bool? enabled = null, CancellationToken ct = default)
        => Ok(await mediator.QueryAsync<RiskRulesQuery, PagedResult<RiskRuleResponse>>(
            new RiskRulesQuery(page, pageSize, scene, enabled), ct));

    /// <summary>创建规则</summary>
    /// <param name="request">规则配置</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 创建的规则；400 — 参数校验失败</returns>
    /// <response code="200">创建成功</response>
    /// <response code="400">参数校验失败</response>
    [HttpPost("rules")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RiskRuleResponse>> CreateRule([FromBody] SaveRiskRuleRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<CreateRiskRuleCommand, RiskRuleResponse>(
                new CreateRiskRuleCommand(request), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>更新规则</summary>
    /// <param name="id">规则 ID</param>
    /// <param name="request">规则配置</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的规则；400 — 参数校验失败；404 — 规则不存在</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">参数校验失败</response>
    /// <response code="404">规则不存在</response>
    [HttpPut("rules/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RiskRuleResponse>> UpdateRule(Guid id, [FromBody] SaveRiskRuleRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<UpdateRiskRuleCommand, RiskRuleResponse>(
                new UpdateRiskRuleCommand(id, request), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "风控规则不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>删除规则</summary>
    /// <param name="id">规则 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 删除成功；404 — 规则不存在</returns>
    /// <response code="200">删除成功</response>
    /// <response code="404">规则不存在</response>
    [HttpDelete("rules/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteRule(Guid id, CancellationToken ct)
    {
        try
        {
            await mediator.SendAsync<DeleteRiskRuleCommand, Unit>(new DeleteRiskRuleCommand(id), ct);
            return Ok(new { message = "规则已删除" });
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "风控规则不存在" });
        }
    }

    /// <summary>启用 / 停用规则</summary>
    /// <param name="id">规则 ID</param>
    /// <param name="enabled">是否启用</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的规则；404 — 规则不存在</returns>
    /// <response code="200">操作成功</response>
    /// <response code="404">规则不存在</response>
    [HttpPut("rules/{id:guid}/enabled")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RiskRuleResponse>> SetRuleEnabled(
        Guid id, [FromQuery] bool enabled, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<SetRiskRuleEnabledCommand, RiskRuleResponse>(
                new SetRiskRuleEnabledCommand(id, enabled), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "风控规则不存在" });
        }
    }

    // ─────────────────── 风险案例 ───────────────────

    /// <summary>案例列表（分页，可按状态/场景/商户/处置级别过滤）</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="status">状态过滤（可选：open/reviewing/resolved/falsepositive）</param>
    /// <param name="scene">场景过滤（可选）</param>
    /// <param name="merchantId">商户过滤（可选）</param>
    /// <param name="disposition">处置级别过滤（可选：watch/block）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 案例分页列表</returns>
    /// <response code="200">案例列表</response>
    [HttpGet("cases")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RiskCaseResponse>>> ListCases(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null, [FromQuery] string? scene = null,
        [FromQuery] Guid? merchantId = null, [FromQuery] RiskDisposition? disposition = null, CancellationToken ct = default)
        => Ok(await mediator.QueryAsync<RiskCasesQuery, PagedResult<RiskCaseResponse>>(
            new RiskCasesQuery(page, pageSize, status, scene, merchantId, disposition), ct));

    /// <summary>开始复核案例（Open → Reviewing）</summary>
    /// <param name="id">案例 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的案例；400 — 状态不允许；404 — 案例不存在</returns>
    /// <response code="200">操作成功</response>
    /// <response code="400">状态不允许</response>
    /// <response code="404">案例不存在</response>
    [HttpPost("cases/{id:guid}/review")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RiskCaseResponse>> StartReview(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<StartReviewRiskCaseCommand, RiskCaseResponse>(
                new StartReviewRiskCaseCommand(id), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "风险案例不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>确认风险（Open/Reviewing → Resolved）</summary>
    /// <param name="id">案例 ID</param>
    /// <param name="request">处置备注（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的案例；400 — 状态不允许；404 — 案例不存在</returns>
    /// <response code="200">处置成功</response>
    /// <response code="400">状态不允许</response>
    /// <response code="404">案例不存在</response>
    [HttpPost("cases/{id:guid}/resolve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RiskCaseResponse>> Resolve(Guid id, [FromBody] ResolveRiskCaseRequest? request, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<ResolveRiskCaseCommand, RiskCaseResponse>(
                new ResolveRiskCaseCommand(id, request?.Note), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "风险案例不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>标记误报（Open/Reviewing → FalsePositive）</summary>
    /// <param name="id">案例 ID</param>
    /// <param name="request">误报说明（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的案例；400 — 状态不允许；404 — 案例不存在</returns>
    /// <response code="200">标记成功</response>
    /// <response code="400">状态不允许</response>
    /// <response code="404">案例不存在</response>
    [HttpPost("cases/{id:guid}/false-positive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RiskCaseResponse>> MarkFalsePositive(Guid id, [FromBody] ResolveRiskCaseRequest? request, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<MarkFalsePositiveRiskCaseCommand, RiskCaseResponse>(
                new MarkFalsePositiveRiskCaseCommand(id, request?.Note), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "风险案例不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    // ─────────────────── 黑名单 ───────────────────

    /// <summary>黑名单列表（分页，可按类型/启用状态过滤）</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="targetType">对象类型过滤（可选）</param>
    /// <param name="enabled">启用状态过滤（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 黑名单分页列表</returns>
    /// <response code="200">黑名单列表</response>
    [HttpGet("blacklist")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<BlacklistResponse>>> ListBlacklist(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] BlacklistTargetType? targetType = null, [FromQuery] bool? enabled = null, CancellationToken ct = default)
        => Ok(await mediator.QueryAsync<BlacklistQuery, PagedResult<BlacklistResponse>>(
            new BlacklistQuery(page, pageSize, targetType, enabled), ct));

    /// <summary>加入黑名单（同对象已存在则更新原因/有效期并重新启用）</summary>
    /// <param name="request">黑名单配置</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 黑名单条目；400 — 参数校验失败</returns>
    /// <response code="200">操作成功</response>
    /// <response code="400">参数校验失败</response>
    [HttpPost("blacklist")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BlacklistResponse>> AddBlacklist([FromBody] SaveBlacklistRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<AddBlacklistCommand, BlacklistResponse>(
                new AddBlacklistCommand(request), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>移除黑名单</summary>
    /// <param name="id">黑名单 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 删除成功；404 — 条目不存在</returns>
    /// <response code="200">删除成功</response>
    /// <response code="404">条目不存在</response>
    [HttpDelete("blacklist/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveBlacklist(Guid id, CancellationToken ct)
    {
        try
        {
            await mediator.SendAsync<RemoveBlacklistCommand, Unit>(new RemoveBlacklistCommand(id), ct);
            return Ok(new { message = "黑名单已移除" });
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "黑名单条目不存在" });
        }
    }

    /// <summary>启用 / 停用黑名单</summary>
    /// <param name="id">黑名单 ID</param>
    /// <param name="enabled">是否启用</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的条目；404 — 条目不存在</returns>
    /// <response code="200">操作成功</response>
    /// <response code="404">条目不存在</response>
    [HttpPut("blacklist/{id:guid}/enabled")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BlacklistResponse>> SetBlacklistEnabled(
        Guid id, [FromQuery] bool enabled, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<SetBlacklistEnabledCommand, BlacklistResponse>(
                new SetBlacklistEnabledCommand(id, enabled), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "黑名单条目不存在" });
        }
    }

    // ─────────────────── 事件流水 ───────────────────

    /// <summary>事件流水列表（分页，可按场景/用户/商户过滤）</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="scene">场景过滤（可选）</param>
    /// <param name="userId">用户过滤（可选）</param>
    /// <param name="merchantId">商户过滤（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 事件分页列表</returns>
    /// <response code="200">事件列表</response>
    [HttpGet("events")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RiskEventResponse>>> ListEvents(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? scene = null, [FromQuery] Guid? userId = null,
        [FromQuery] Guid? merchantId = null, CancellationToken ct = default)
        => Ok(await mediator.QueryAsync<RiskEventsQuery, PagedResult<RiskEventResponse>>(
            new RiskEventsQuery(page, pageSize, scene, userId, merchantId), ct));
}

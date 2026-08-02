using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using BuildingBlocks.MultiTenant;
using SettlementService.Application.Queries;
using SettlementService.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SettlementService.Controllers;

/// <summary>
/// 商户结算接口（商户端）— 我的结算单 / 详情 / 概览 / 佣金比例，需 X-Merchant-Id 请求头。
/// </summary>
[ApiController]
[Authorize]
[Route("api/settlements")]
[Produces("application/json")]
public sealed class MerchantSettlementsController(IMediator mediator, ITenantProvider tenantProvider) : ControllerBase
{
    /// <summary>当前商户 ID（缺商户上下文抛业务异常）</summary>
    private Guid MerchantId => tenantProvider.CurrentMerchantId
        ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

    /// <summary>我的结算单列表（分页，可按状态过滤）</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="status">状态过滤（可选：pending/settled/paid）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 结算单分页列表</returns>
    /// <response code="200">结算单列表</response>
    /// <response code="400">缺商户上下文</response>
    [HttpGet("merchant")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<SettlementResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null, CancellationToken ct = default)
    {
        try
        {
            return Ok(await mediator.QueryAsync<MerchantSettlementsQuery, PagedResult<SettlementResponse>>(
                new MerchantSettlementsQuery(MerchantId, page, pageSize, status), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>结算单详情（含明细）</summary>
    /// <param name="id">结算单 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 结算单详情</returns>
    /// <response code="200">结算单详情</response>
    /// <response code="400">缺商户上下文</response>
    /// <response code="404">结算单不存在或不属于当前商户</response>
    [HttpGet("merchant/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SettlementResponse>> Detail(Guid id, CancellationToken ct = default)
    {
        try
        {
            var result = await mediator.QueryAsync<MerchantSettlementDetailQuery, SettlementResponse?>(
                new MerchantSettlementDetailQuery(MerchantId, id), ct);
            return result is null ? NotFound(new { error = "结算单不存在" }) : Ok(result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>结算概览（待结算/已结算金额与单数）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 概览数据</returns>
    /// <response code="200">概览数据</response>
    /// <response code="400">缺商户上下文</response>
    [HttpGet("merchant/summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MerchantSettlementSummaryResponse>> Summary(CancellationToken ct = default)
    {
        try
        {
            return Ok(await mediator.QueryAsync<MerchantSettlementSummaryQuery, MerchantSettlementSummaryResponse>(
                new MerchantSettlementSummaryQuery(MerchantId), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>我的佣金比例（未配置返回平台默认）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 佣金规则</returns>
    /// <response code="200">佣金规则</response>
    /// <response code="400">缺商户上下文</response>
    [HttpGet("merchant/commission")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CommissionRuleResponse>> Commission(CancellationToken ct = default)
    {
        try
        {
            return Ok(await mediator.QueryAsync<MerchantCommissionRuleQuery, CommissionRuleResponse>(
                new MerchantCommissionRuleQuery(MerchantId), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }
}

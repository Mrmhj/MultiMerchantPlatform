using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using BuildingBlocks.MultiTenant;
using LogisticsService.Application.Commands;
using LogisticsService.Application.Queries;
using LogisticsService.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogisticsService.Controllers;

/// <summary>
/// 商户运单接口（商户端）— 我的运单列表 / 详情 / 启用物流公司，需 X-Merchant-Id 请求头。
/// </summary>
[ApiController]
[Authorize]
[Route("api/logistics/shipments")]
[Produces("application/json")]
public sealed class MerchantShipmentsController(IMediator mediator, ITenantProvider tenantProvider) : ControllerBase
{
    /// <summary>当前商户 ID（缺商户上下文抛业务异常）</summary>
    private Guid MerchantId => tenantProvider.CurrentMerchantId
        ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

    /// <summary>我的运单列表（分页，可按状态过滤）</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="status">状态过滤（可选：created/intransit/outfordelivery/signed/exception）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 运单分页列表</returns>
    /// <response code="200">运单列表</response>
    /// <response code="400">缺商户上下文</response>
    [HttpGet("merchant")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ShipmentResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null, CancellationToken ct = default)
    {
        try
        {
            return Ok(await mediator.QueryAsync<MerchantShipmentsQuery, PagedResult<ShipmentResponse>>(
                new MerchantShipmentsQuery(MerchantId, page, pageSize, status), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>运单详情（含轨迹）</summary>
    /// <param name="id">运单 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 运单详情</returns>
    /// <response code="200">运单详情</response>
    /// <response code="400">缺商户上下文</response>
    /// <response code="404">运单不存在或不属于当前商户</response>
    [HttpGet("merchant/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShipmentResponse>> Detail(Guid id, CancellationToken ct = default)
    {
        try
        {
            var result = await mediator.QueryAsync<MerchantShipmentDetailQuery, ShipmentResponse?>(
                new MerchantShipmentDetailQuery(MerchantId, id), ct);
            return result is null ? NotFound(new { error = "运单不存在" }) : Ok(result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>启用物流公司列表（发货时选择）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 启用物流公司列表</returns>
    /// <response code="200">公司列表</response>
    [HttpGet("merchant/companies")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CompanyResponse>>> Companies(CancellationToken ct = default)
    {
        return Ok(await mediator.QueryAsync<EnabledCompaniesQuery, List<CompanyResponse>>(
            new EnabledCompaniesQuery(), ct));
    }
}

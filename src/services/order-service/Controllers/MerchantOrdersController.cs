using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using OrderService.Application.Commands;
using OrderService.Application.Queries;
using OrderService.Domain.Enums;
using OrderService.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OrderService.Controllers;

/// <summary>
/// 商户订单 API — 商户维度子订单（列表/发货/完成，需 X-Merchant-Id）。
/// </summary>
[ApiController]
[Authorize]
[Route("api/orders/merchant")]
[Produces("application/json")]
public sealed class MerchantOrdersController(IMediator mediator) : ControllerBase
{
    /// <summary>商户订单列表（当前商户，子单维度，状态过滤）</summary>
    /// <param name="status">按状态过滤（可选：1待付款 2已付款 3已发货 4已完成 5已取消）</param>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 分页子订单列表；400 — 缺商户上下文</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<SubOrderResponse>>> List(
        [FromQuery] SubOrderStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            var query = new ListMerchantSubOrdersQuery(status, page, pageSize);
            return Ok(await mediator.QueryAsync<ListMerchantSubOrdersQuery, PagedResult<SubOrderResponse>>(query, ct));
        }
        catch (DomainException ex) when (ex.ErrorCode == "MERCHANT_REQUIRED")
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>子订单发货（已付款后操作，需携带物流公司编码与运单号，自动创建物流运单）</summary>
    /// <param name="id">子订单 ID</param>
    /// <param name="request">物流信息（物流公司编码 + 运单号）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 发货后的子订单；400 — 状态不允许或缺商户上下文；404 — 子订单不存在</returns>
    [HttpPost("{id:guid}/ship")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubOrderResponse>> Ship(
        Guid id, [FromBody] ShipSubOrderRequest request, CancellationToken ct)
    {
        try
        {
            var command = new ShipSubOrderCommand(id, request.CarrierCode, request.TrackingNo);
            return Ok(await mediator.SendAsync<ShipSubOrderCommand, SubOrderResponse>(command, ct));
        }
        catch (NotFoundException)
        {
            return NotFound("子订单不存在");
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>子订单完成（发货后确认收货）</summary>
    /// <param name="id">子订单 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 完成后的子订单；400 — 状态不允许；404 — 子订单不存在</returns>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubOrderResponse>> Complete(Guid id, CancellationToken ct)
    {
        try
        {
            var command = new CompleteSubOrderCommand(id);
            return Ok(await mediator.SendAsync<CompleteSubOrderCommand, SubOrderResponse>(command, ct));
        }
        catch (NotFoundException)
        {
            return NotFound("子订单不存在");
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

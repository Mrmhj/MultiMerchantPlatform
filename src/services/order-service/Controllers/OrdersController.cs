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
/// 订单 API — 买家订单（创建/列表/详情/取消/支付确认）。
/// </summary>
[ApiController]
[Authorize]
[Route("api/orders")]
[Produces("application/json")]
public sealed class OrdersController(IMediator mediator) : ControllerBase
{
    /// <summary>创建订单（商品可跨商户，自动拆单；初始待付款）</summary>
    /// <param name="request">创建订单请求（商品项 ≥ 1）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 订单（含拆单结果）</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderResponse>> Create([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        try
        {
            var command = new CreateOrderCommand(request.Items, request.Remark);
            return Created("", await mediator.SendAsync<CreateOrderCommand, OrderResponse>(command, ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>我的订单分页列表</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 分页订单列表</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new ListMyOrdersQuery(page, pageSize);
        return Ok(await mediator.QueryAsync<ListMyOrdersQuery, PagedResult<OrderResponse>>(query, ct));
    }

    /// <summary>订单详情（含拆单与商品项）</summary>
    /// <param name="id">订单 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 订单详情；403 — 非本人订单；404 — 订单不存在</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.QueryAsync<GetOrderQuery, OrderResponse>(new GetOrderQuery(id), ct));
        }
        catch (NotFoundException)
        {
            return NotFound("订单不存在");
        }
        catch (DomainException ex) when (ex.ErrorCode == "FORBIDDEN")
        {
            return Forbid();
        }
    }

    /// <summary>取消订单（仅待付款可取消，级联取消子单）</summary>
    /// <param name="id">订单 ID</param>
    /// <param name="reason">取消原因（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 取消后的订单；400 — 状态不允许；404 — 订单不存在</returns>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> Cancel(Guid id, [FromQuery] string? reason, CancellationToken ct)
    {
        try
        {
            var command = new CancelOrderCommand(id, reason);
            return Ok(await mediator.SendAsync<CancelOrderCommand, OrderResponse>(command, ct));
        }
        catch (NotFoundException)
        {
            return NotFound("订单不存在");
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

    /// <summary>订单支付确认（模拟支付回调；正式版由 pay-service 回调）</summary>
    /// <param name="id">订单 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 支付后的订单；400 — 状态不允许；404 — 订单不存在</returns>
    [HttpPost("{id:guid}/pay")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> Pay(Guid id, CancellationToken ct)
    {
        try
        {
            var command = new MarkOrderPaidCommand(id);
            return Ok(await mediator.SendAsync<MarkOrderPaidCommand, OrderResponse>(command, ct));
        }
        catch (NotFoundException)
        {
            return NotFound("订单不存在");
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

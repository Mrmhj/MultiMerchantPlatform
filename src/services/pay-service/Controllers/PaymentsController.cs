using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using PayService.Application.Commands;
using PayService.Application.Queries;
using PayService.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PayService.Controllers;

/// <summary>
/// 支付 API — 支付单创建 / 模拟支付（成功后回调订单）/ 退款 / 查询。
/// </summary>
[ApiController]
[Authorize]
[Route("api/payments")]
[Produces("application/json")]
public sealed class PaymentsController(IMediator mediator) : ControllerBase
{
    /// <summary>创建支付单（关联订单，状态待支付）</summary>
    /// <param name="request">创建支付单请求（订单 ID + 金额）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 支付单；400 — 重复支付单</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentResponse>> Create([FromBody] CreatePaymentRequest request, CancellationToken ct)
    {
        try
        {
            var command = new CreatePaymentCommand(request.OrderId, request.Amount);
            return Created("", await mediator.SendAsync<CreatePaymentCommand, PaymentResponse>(command, ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>我的支付单分页列表</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 分页支付单列表</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PaymentResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new ListMyPaymentsQuery(page, pageSize);
        return Ok(await mediator.QueryAsync<ListMyPaymentsQuery, PagedResult<PaymentResponse>>(query, ct));
    }

    /// <summary>支付单详情</summary>
    /// <param name="id">支付单 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 支付单；404 — 不存在</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentResponse>> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.QueryAsync<GetPaymentQuery, PaymentResponse>(new GetPaymentQuery(id), ct));
        }
        catch (NotFoundException)
        {
            return NotFound("支付单不存在");
        }
    }

    /// <summary>模拟支付 — 模拟渠道支付成功，并通知 order-service 确认订单已支付</summary>
    /// <param name="id">支付单 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 支付成功后的支付单；400 — 状态不允许</returns>
    [HttpPost("{id:guid}/simulate-pay")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentResponse>> SimulatePay(Guid id, CancellationToken ct)
    {
        try
        {
            var command = new SimulatePayCommand(id);
            return Ok(await mediator.SendAsync<SimulatePayCommand, PaymentResponse>(command, ct));
        }
        catch (NotFoundException)
        {
            return NotFound("支付单不存在");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>退款（仅支付成功后）</summary>
    /// <param name="id">支付单 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 退款后的支付单；400 — 状态不允许</returns>
    [HttpPost("{id:guid}/refund")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentResponse>> Refund(Guid id, CancellationToken ct)
    {
        try
        {
            var command = new RefundCommand(id);
            return Ok(await mediator.SendAsync<RefundCommand, PaymentResponse>(command, ct));
        }
        catch (NotFoundException)
        {
            return NotFound("支付单不存在");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

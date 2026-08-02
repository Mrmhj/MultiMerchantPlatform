using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using OrderService.Application.Commands;
using OrderService.Domain.Enums;
using OrderService.DTOs;
using OrderService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace OrderService.Controllers;

/// <summary>
/// 内部接口（服务间调用）— 供 pay-service 支付成功回调、settlement-service 结算取数，X-Internal-Key 校验，不走买家鉴权。
/// </summary>
[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public sealed class InternalOrdersController(
    IMediator mediator,
    IConfiguration configuration,
    OrderDbContext db) : ControllerBase
{
    private readonly string _internalKey = configuration["Internal:Key"] ?? string.Empty;

    /// <summary>内部支付确认（pay-service 支付成功后回调，请求头 X-Internal-Key）</summary>
    /// <param name="id">订单 ID</param>
    /// <param name="key">内部密钥（请求头 X-Internal-Key）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 支付后的订单；400 — 状态不允许；401 — 内部密钥无效；404 — 订单不存在</returns>
    [HttpPost("{id:guid}/pay-internal")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> PayInternal(
        Guid id,
        [FromHeader(Name = "X-Internal-Key")] string key,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_internalKey) || key != _internalKey)
            return Unauthorized(new { error = "内部密钥无效" });

        try
        {
            var command = new MarkOrderPaidInternalCommand(id);
            return Ok(await mediator.SendAsync<MarkOrderPaidInternalCommand, OrderResponse>(command, ct));
        }
        catch (NotFoundException)
        {
            return NotFound("订单不存在");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>内部已完成子订单查询（settlement-service 生成结算单数据源，按完成时间过滤）</summary>
    /// <param name="start">周期开始（UTC，可选）</param>
    /// <param name="end">周期结束（UTC，可选）</param>
    /// <param name="key">内部密钥（请求头 X-Internal-Key）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 已完成子订单列表；401 — 内部密钥无效</returns>
    /// <response code="200">已完成子订单列表</response>
    /// <response code="401">内部密钥无效</response>
    [HttpGet("internal/completed-suborders")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<CompletedSubOrderDto>>> CompletedSubOrders(
        [FromQuery] DateTime? start, [FromQuery] DateTime? end,
        [FromHeader(Name = "X-Internal-Key")] string key,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_internalKey) || key != _internalKey)
            return Unauthorized(new { error = "内部密钥无效" });

        var items = await (
            from s in db.SubOrders.AsNoTracking()
            where s.Status == SubOrderStatus.Completed
                && (!start.HasValue || s.UpdatedAt >= start)
                && (!end.HasValue || s.UpdatedAt <= end)
            join o in db.Orders.AsNoTracking() on s.OrderId equals o.Id
            orderby s.UpdatedAt ?? s.CreatedAt
            select new CompletedSubOrderDto(
                s.Id, s.OrderId, o.OrderNo, s.MerchantId, s.MerchantName,
                s.TotalAmount, s.UpdatedAt ?? s.CreatedAt))
            .ToListAsync(ct);

        return Ok(items);
    }
}

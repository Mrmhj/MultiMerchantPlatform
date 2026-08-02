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

    /// <summary>内部 BI 统计（bi-admin 服务聚合数据源，X-Internal-Key 校验）</summary>
    /// <param name="start">周期开始（UTC，可选，默认近 30 天）</param>
    /// <param name="end">周期结束（UTC，可选）</param>
    /// <param name="key">内部密钥（请求头 X-Internal-Key）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — BI 订单统计；401 — 内部密钥无效</returns>
    /// <response code="200">BI 订单统计（总览/按天销售/商户排行/商品排行/状态分布）</response>
    /// <response code="401">内部密钥无效</response>
    [HttpGet("internal/bi-stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BiOrderStatsResponse>> BiStats(
        [FromQuery] DateTime? start, [FromQuery] DateTime? end,
        [FromHeader(Name = "X-Internal-Key")] string key,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_internalKey) || key != _internalKey)
            return Unauthorized(new { error = "内部密钥无效" });

        // 销售口径：已付款（含已发货/已完成）计入 GMV
        var effectiveStatuses = new[]
        {
            SubOrderStatus.Paid, SubOrderStatus.Shipped, SubOrderStatus.Completed,
        };
        var from = start ?? DateTime.UtcNow.AddDays(-29).Date;
        var to = end ?? DateTime.UtcNow.Date.AddDays(1);

        // 1. 总览
        var totalGmv = await db.SubOrders.AsNoTracking()
            .Where(s => effectiveStatuses.Contains(s.Status))
            .SumAsync(s => (decimal?)s.TotalAmount, ct) ?? 0m;
        var totalOrderCount = await db.Orders.AsNoTracking().CountAsync(ct);
        var paidOrderCount = await db.Orders.AsNoTracking()
            .CountAsync(o => o.Status == OrderStatus.Paid || o.Status == OrderStatus.Completed, ct);
        var completedOrderCount = await db.Orders.AsNoTracking()
            .CountAsync(o => o.Status == OrderStatus.Completed, ct);

        // 2. 按天销售（子订单创建日分组）
        var daily = await db.SubOrders.AsNoTracking()
            .Where(s => effectiveStatuses.Contains(s.Status) && s.CreatedAt >= from && s.CreatedAt < to)
            .GroupBy(s => s.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Gmv = g.Sum(s => s.TotalAmount), Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync(ct);

        // 3. 商户销售排行（按子订单商户维度聚合）
        var merchantRank = await db.SubOrders.AsNoTracking()
            .Where(s => effectiveStatuses.Contains(s.Status))
            .GroupBy(s => new { s.MerchantId, s.MerchantName })
            .Select(g => new
            {
                g.Key.MerchantId,
                g.Key.MerchantName,
                Gmv = g.Sum(s => s.TotalAmount),
                Count = g.Count(),
            })
            .OrderByDescending(x => x.Gmv)
            .Take(10)
            .ToListAsync(ct);

        // 4. 商品销售排行（订单项按商品维度聚合；子查询过滤有效状态子单，避免 join+group 翻译问题）
        var productRank = await db.OrderItems.AsNoTracking()
            .Where(i => db.SubOrders.Any(s => s.Id == i.SubOrderId && effectiveStatuses.Contains(s.Status)))
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.ProductName,
                Quantity = g.Sum(i => i.Quantity),
                Amount = g.Sum(i => i.UnitPrice * i.Quantity),
            })
            .OrderByDescending(x => x.Amount)
            .Take(10)
            .ToListAsync(ct);

        // 5. 主订单状态分布
        var orderStatus = await db.Orders.AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new { Status = (int)g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return Ok(new BiOrderStatsResponse(
            totalGmv,
            totalOrderCount,
            paidOrderCount,
            completedOrderCount,
            daily.Select(x => new BiDailySalesPoint(x.Date.ToString("yyyy-MM-dd"), x.Gmv, x.Count)).ToList(),
            merchantRank.Select(x => new BiMerchantRankItem(x.MerchantId, x.MerchantName, x.Gmv, x.Count)).ToList(),
            productRank.Select(x => new BiProductRankItem(x.ProductId, x.ProductName, x.Quantity, x.Amount)).ToList(),
            orderStatus.Select(x => new BiOrderStatusItem(x.Status, x.Count)).ToList()));
    }
}

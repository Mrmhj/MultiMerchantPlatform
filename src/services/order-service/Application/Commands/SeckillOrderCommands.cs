using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using OrderService.Domain.Entities;
using OrderService.DTOs;
using OrderService.Infrastructure;
using OrderService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace OrderService.Application.Commands;

/// <summary>秒杀订单创建命令（内部，order-service 消费秒杀消息时调用）</summary>
/// <param name="SeckillRecordId">秒杀记录 ID（幂等键）</param>
/// <param name="BuyerUserId">买家用户 ID（来自消息，非 JWT）</param>
/// <param name="MerchantId">商户 ID（快照）</param>
/// <param name="MerchantName">商户名称（快照）</param>
/// <param name="ProductId">商品 ID（快照）</param>
/// <param name="ProductName">商品名称（快照）</param>
/// <param name="SkuId">SKU ID（快照）</param>
/// <param name="SkuCode">SKU 编码（快照）</param>
/// <param name="Spec">规格（快照）</param>
/// <param name="UnitPrice">秒杀价（快照）</param>
/// <param name="Quantity">购买数量</param>
public sealed record CreateSeckillOrderCommand(
    Guid SeckillRecordId, Guid BuyerUserId,
    Guid MerchantId, string MerchantName,
    Guid ProductId, string ProductName,
    Guid SkuId, string SkuCode, string Spec,
    decimal UnitPrice, int Quantity) : ICommand<Result<OrderResponse>>;

/// <summary>秒杀订单创建命令处理器 — 幂等（秒杀记录唯一）→ 预占库存 → 创建订单 → 幂等记录 → 回调 promotion 标记</summary>
public sealed class CreateSeckillOrderCommandHandler(
    OrderDbContext db,
    StockServiceClient stockClient,
    PromotionSeckillClient promotionSeckillClient,
    ILogger<CreateSeckillOrderCommandHandler> logger) : ICommandHandler<CreateSeckillOrderCommand, Result<OrderResponse>>
{
    /// <inheritdoc />
    public async Task<Result<OrderResponse>> HandleAsync(
        CreateSeckillOrderCommand command, CancellationToken ct = default)
    {
        // ① 幂等：该秒杀记录已处理过则直接返回既有订单（防消息重投重复下单）
        var existing = await db.SeckillOrderProcesseds.AsNoTracking()
            .FirstOrDefaultAsync(p => p.SeckillRecordId == command.SeckillRecordId, ct);
        if (existing is not null)
        {
            var order = await db.Orders
                .Include(o => o.SubOrders).ThenInclude(s => s.Items)
                .FirstOrDefaultAsync(o => o.Id == existing.OrderId, ct);
            return order is null
                ? Result.Failure<OrderResponse>($"幂等记录存在但订单缺失 {existing.OrderId}")
                : Result.Success(OrderMapper.ToResponse(order));
        }

        var orderNo = GenerateOrderNo();

        // ② 预占库存（秒杀场景库存已在 Redis 预扣，此处预占真实库存；失败 → 不建单）
        var reserve = await stockClient.ReserveAsync(command.SkuId, command.Quantity, orderNo, ct);
        if (!reserve.IsSuccess)
        {
            logger.LogWarning("秒杀订单库存预占失败(HTTP) SkuId={SkuId} Qty={Qty} Error={Error}",
                command.SkuId, command.Quantity, reserve.Error);
            return Result.Failure<OrderResponse>(reserve.Error ?? "库存服务调用失败");
        }
        if (!reserve.Value.Success)
        {
            logger.LogWarning("秒杀订单库存预占失败(业务) SkuId={SkuId} Qty={Qty} Error={Error}",
                command.SkuId, command.Quantity, reserve.Value.Error);
            return Result.Failure<OrderResponse>(reserve.Value.Error ?? "库存不足");
        }

        try
        {
            // ③ 创建订单（单一 SKU，秒杀价）
            var input = new OrderItemInput(
                command.MerchantId, command.MerchantName,
                command.ProductId, command.ProductName,
                command.SkuId, command.SkuCode, command.Spec,
                command.UnitPrice, command.Quantity);
            var order = Order.Create(command.BuyerUserId, orderNo, [input]);
            db.Orders.Add(order);

            // ④ 幂等记录（与订单同事务）
            db.SeckillOrderProcesseds.Add(new SeckillOrderProcessed(
                command.SeckillRecordId, order.Id, orderNo));

            await db.SaveChangesAsync(ct);

            // ⑤ 回调 promotion-service 标记秒杀记录 Ordered（失败仅记录日志，不影响订单落库）
            var mark = await promotionSeckillClient.MarkOrderedAsync(command.SeckillRecordId, order.Id, orderNo, ct);
            if (!mark)
                logger.LogWarning("秒杀记录标记订单失败 RecordId={RecordId} OrderNo={OrderNo}",
                    command.SeckillRecordId, orderNo);

            return Result.Success(OrderMapper.ToResponse(order));
        }
        catch
        {
            // 建单失败 → 释放预占库存（补偿）
            try { await stockClient.ReleaseAsync(command.SkuId, command.Quantity, orderNo, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "秒杀建单失败释放库存异常 SkuId={SkuId}", command.SkuId); }
            throw;
        }
    }

    private static string GenerateOrderNo()
        => $"ORD{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
}

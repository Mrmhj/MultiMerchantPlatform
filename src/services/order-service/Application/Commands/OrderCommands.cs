using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using BuildingBlocks.Security;
using OrderService.Domain.Entities;
using OrderService.DTOs;
using OrderService.Infrastructure;
using OrderService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace OrderService.Application.Commands;

/// <summary>创建订单命令（按商户拆单，初始待付款；下单预占库存）</summary>
public sealed record CreateOrderCommand(
    List<OrderItemRequest> Items,
    string? Remark) : ICommand<OrderResponse>;

/// <summary>创建订单命令处理器</summary>
public sealed class CreateOrderCommandHandler(
    OrderDbContext db,
    ICurrentUser currentUser,
    StockServiceClient stockClient,
    ILogger<CreateOrderCommandHandler> logger) : ICommandHandler<CreateOrderCommand, OrderResponse>
{
    /// <inheritdoc />
    public async Task<OrderResponse> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty)
            throw new DomainException("请先登录", "UNAUTHENTICATED");

        var inputs = command.Items.Select(i => new OrderItemInput(
            i.MerchantId, i.MerchantName, i.ProductId, i.ProductName,
            i.SkuId, i.SkuCode, i.Spec, i.UnitPrice, i.Quantity)).ToList();

        var orderNo = GenerateOrderNo();

        // ① 预占库存（逐项；任一失败 → 释放已预占并拒绝下单）
        var reserved = new List<(Guid SkuId, int Quantity)>();
        try
        {
            foreach (var item in inputs)
            {
                var result = await stockClient.ReserveAsync(item.SkuId, item.Quantity, orderNo, ct);
                if (!result.IsSuccess)
                    throw new DomainException("库存服务调用失败", "STOCK_SERVICE_ERROR");
                if (!result.Value.Success)
                    throw new DomainException(result.Value.Error ?? "库存不足", "STOCK_INSUFFICIENT");
                reserved.Add((item.SkuId, item.Quantity));
            }
        }
        catch
        {
            // 补偿：释放已预占库存
            foreach (var (skuId, qty) in reserved)
            {
                try { await stockClient.ReleaseAsync(skuId, qty, orderNo, ct); }
                catch (Exception ex) { logger.LogWarning(ex, "下单失败补偿释放库存异常 SkuId={SkuId}", skuId); }
            }
            throw;
        }

        // ② 创建订单（拆单）
        var order = Order.Create(currentUser.UserId, orderNo, inputs, command.Remark);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        return OrderMapper.ToResponse(order);
    }

    private static string GenerateOrderNo()
        => $"ORD{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
}

/// <summary>取消订单命令（仅待付款；释放预占库存）</summary>
public sealed record CancelOrderCommand(Guid OrderId, string? Reason) : ICommand<OrderResponse>;

/// <summary>取消订单命令处理器</summary>
public sealed class CancelOrderCommandHandler(
    OrderDbContext db,
    ICurrentUser currentUser,
    StockServiceClient stockClient,
    ILogger<CancelOrderCommandHandler> logger) : ICommandHandler<CancelOrderCommand, OrderResponse>
{
    /// <inheritdoc />
    public async Task<OrderResponse> HandleAsync(CancelOrderCommand command, CancellationToken ct = default)
    {
        var order = await db.Orders
            .Include(o => o.SubOrders).ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, ct)
            ?? throw new NotFoundException("订单", command.OrderId);

        if (order.BuyerUserId != currentUser.UserId)
            throw new DomainException("无权操作该订单", "FORBIDDEN");

        order.Cancel(command.Reason);

        // 释放预占库存（失败记录日志，不阻塞取消）
        foreach (var item in order.SubOrders.SelectMany(s => s.Items))
        {
            try { await stockClient.ReleaseAsync(item.SkuId, item.Quantity, order.OrderNo, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "取消订单释放库存失败 SkuId={SkuId}", item.SkuId); }
        }

        await db.SaveChangesAsync(ct);
        return OrderMapper.ToResponse(order);
    }
}

/// <summary>订单支付确认命令（模拟支付回调；确认后扣减库存）</summary>
public sealed record MarkOrderPaidCommand(Guid OrderId) : ICommand<OrderResponse>;

/// <summary>订单支付确认命令处理器</summary>
public sealed class MarkOrderPaidCommandHandler(
    OrderDbContext db,
    ICurrentUser currentUser,
    StockServiceClient stockClient,
    ILogger<MarkOrderPaidCommandHandler> logger) : ICommandHandler<MarkOrderPaidCommand, OrderResponse>
{
    /// <inheritdoc />
    public async Task<OrderResponse> HandleAsync(MarkOrderPaidCommand command, CancellationToken ct = default)
    {
        var order = await db.Orders
            .Include(o => o.SubOrders).ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, ct)
            ?? throw new NotFoundException("订单", command.OrderId);

        if (order.BuyerUserId != currentUser.UserId)
            throw new DomainException("无权操作该订单", "FORBIDDEN");

        order.MarkPaid();

        // 扣减库存（预占转扣减；失败记录日志，不阻塞支付确认）
        await ConfirmStockAsync(order, ct);

        await db.SaveChangesAsync(ct);
        return OrderMapper.ToResponse(order);
    }

    /// <summary>逐项确认扣减库存</summary>
    private async Task ConfirmStockAsync(Order order, CancellationToken ct)
    {
        foreach (var item in order.SubOrders.SelectMany(s => s.Items))
        {
            try { await stockClient.ConfirmAsync(item.SkuId, item.Quantity, order.OrderNo, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "支付确认扣减库存失败 SkuId={SkuId}", item.SkuId); }
        }
    }
}

/// <summary>内部支付确认命令（pay-service 回调；确认后扣减库存）</summary>
public sealed record MarkOrderPaidInternalCommand(Guid OrderId) : ICommand<OrderResponse>;

/// <summary>内部支付确认命令处理器</summary>
public sealed class MarkOrderPaidInternalCommandHandler(
    OrderDbContext db,
    StockServiceClient stockClient,
    ILogger<MarkOrderPaidInternalCommandHandler> logger) : ICommandHandler<MarkOrderPaidInternalCommand, OrderResponse>
{
    /// <inheritdoc />
    public async Task<OrderResponse> HandleAsync(MarkOrderPaidInternalCommand command, CancellationToken ct = default)
    {
        var order = await db.Orders
            .Include(o => o.SubOrders).ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, ct)
            ?? throw new NotFoundException("订单", command.OrderId);

        order.MarkPaid();

        // 扣减库存（失败记录日志，不阻塞支付确认）
        foreach (var item in order.SubOrders.SelectMany(s => s.Items))
        {
            try { await stockClient.ConfirmAsync(item.SkuId, item.Quantity, order.OrderNo, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "内部支付确认扣减库存失败 SkuId={SkuId}", item.SkuId); }
        }

        await db.SaveChangesAsync(ct);
        return OrderMapper.ToResponse(order);
    }
}

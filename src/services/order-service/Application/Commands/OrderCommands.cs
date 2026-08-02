using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Security;
using OrderService.Domain.Entities;
using OrderService.DTOs;
using OrderService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace OrderService.Application.Commands;

/// <summary>创建订单命令（按商户拆单，初始待付款）</summary>
public sealed record CreateOrderCommand(
    List<OrderItemRequest> Items,
    string? Remark) : ICommand<OrderResponse>;

/// <summary>创建订单命令处理器</summary>
public sealed class CreateOrderCommandHandler(
    OrderDbContext db,
    ICurrentUser currentUser) : ICommandHandler<CreateOrderCommand, OrderResponse>
{
    /// <inheritdoc />
    public async Task<OrderResponse> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty)
            throw new DomainException("请先登录", "UNAUTHENTICATED");

        var inputs = command.Items.Select(i => new OrderItemInput(
            i.MerchantId, i.MerchantName, i.ProductId, i.ProductName,
            i.SkuId, i.SkuCode, i.Spec, i.UnitPrice, i.Quantity)).ToList();

        var order = Order.Create(currentUser.UserId, GenerateOrderNo(), inputs, command.Remark);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);

        return OrderMapper.ToResponse(order);
    }

    private static string GenerateOrderNo()
        => $"ORD{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
}

/// <summary>取消订单命令（仅待付款）</summary>
public sealed record CancelOrderCommand(Guid OrderId, string? Reason) : ICommand<OrderResponse>;

/// <summary>取消订单命令处理器</summary>
public sealed class CancelOrderCommandHandler(
    OrderDbContext db,
    ICurrentUser currentUser) : ICommandHandler<CancelOrderCommand, OrderResponse>
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
        await db.SaveChangesAsync(ct);
        return OrderMapper.ToResponse(order);
    }
}

/// <summary>订单支付确认命令（模拟支付回调；正式对接 pay-service）</summary>
public sealed record MarkOrderPaidCommand(Guid OrderId) : ICommand<OrderResponse>;

/// <summary>订单支付确认命令处理器</summary>
public sealed class MarkOrderPaidCommandHandler(
    OrderDbContext db,
    ICurrentUser currentUser) : ICommandHandler<MarkOrderPaidCommand, OrderResponse>
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
        await db.SaveChangesAsync(ct);
        return OrderMapper.ToResponse(order);
    }
}

/// <summary>内部支付确认命令（pay-service 回调，不校验买家身份，X-Internal-Key 在网关/Controller 层校验）</summary>
public sealed record MarkOrderPaidInternalCommand(Guid OrderId) : ICommand<OrderResponse>;

/// <summary>内部支付确认命令处理器</summary>
public sealed class MarkOrderPaidInternalCommandHandler(OrderDbContext db) : ICommandHandler<MarkOrderPaidInternalCommand, OrderResponse>
{
    /// <inheritdoc />
    public async Task<OrderResponse> HandleAsync(MarkOrderPaidInternalCommand command, CancellationToken ct = default)
    {
        var order = await db.Orders
            .Include(o => o.SubOrders).ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, ct)
            ?? throw new NotFoundException("订单", command.OrderId);

        order.MarkPaid();
        await db.SaveChangesAsync(ct);
        return OrderMapper.ToResponse(order);
    }
}

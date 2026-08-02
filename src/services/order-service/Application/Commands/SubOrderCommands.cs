using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.MultiTenant;
using OrderService.Domain.Enums;
using OrderService.DTOs;
using OrderService.Infrastructure;
using OrderService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace OrderService.Application.Commands;

/// <summary>子订单发货命令（商户操作，X-Merchant-Id）</summary>
/// <param name="SubOrderId">子订单 ID</param>
/// <param name="CarrierCode">物流公司编码</param>
/// <param name="TrackingNo">物流运单号</param>
public sealed record ShipSubOrderCommand(Guid SubOrderId, string CarrierCode, string TrackingNo) : ICommand<SubOrderResponse>;

/// <summary>子订单发货命令处理器（发货成功 → 通知物流服务创建运单，下游失败不阻断发货）</summary>
public sealed class ShipSubOrderCommandHandler(
    OrderDbContext db,
    ITenantProvider tenantProvider,
    LogisticsServiceClient logisticsClient,
    ILogger<ShipSubOrderCommandHandler> logger) : ICommandHandler<ShipSubOrderCommand, SubOrderResponse>
{
    /// <inheritdoc />
    public async Task<SubOrderResponse> HandleAsync(ShipSubOrderCommand command, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var sub = await db.SubOrders
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == command.SubOrderId && s.MerchantId == merchantId, ct)
            ?? throw new NotFoundException("子订单", command.SubOrderId);

        sub.Ship(command.CarrierCode, command.TrackingNo);
        await db.SaveChangesAsync(ct);

        // 发货成功 → 通知物流服务创建运单（物流服务不可用不阻断发货，仅记录日志）
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == sub.OrderId, ct);
        if (order is not null)
        {
            var result = await logisticsClient.CreateShipmentAsync(
                order.BuyerUserId, sub.MerchantId, sub.Id, order.Id, order.OrderNo,
                command.CarrierCode, command.TrackingNo, ct);
            if (!result.IsSuccess)
                logger.LogWarning("创建物流运单失败 SubOrderId={SubOrderId} Error={Error}", sub.Id, result.Error);
        }

        return OrderMapper.ToSubOrderResponse(sub);
    }
}

/// <summary>子订单完成命令（买家确认收货，X-Merchant-Id 校验商户归属）</summary>
public sealed record CompleteSubOrderCommand(Guid SubOrderId) : ICommand<SubOrderResponse>;

/// <summary>子订单完成命令处理器</summary>
public sealed class CompleteSubOrderCommandHandler(
    OrderDbContext db,
    ITenantProvider tenantProvider) : ICommandHandler<CompleteSubOrderCommand, SubOrderResponse>
{
    /// <inheritdoc />
    public async Task<SubOrderResponse> HandleAsync(CompleteSubOrderCommand command, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var sub = await db.SubOrders
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == command.SubOrderId && s.MerchantId == merchantId, ct)
            ?? throw new NotFoundException("子订单", command.SubOrderId);

        sub.Complete();

        // 子单完成 → 尝试完成主订单（必须加载全部子单，避免 EF 关系修复用不完整集合误判）
        var order = await db.Orders
            .Include(o => o.SubOrders)
            .FirstOrDefaultAsync(o => o.Id == sub.OrderId, ct);
        order?.TryComplete();

        await db.SaveChangesAsync(ct);
        return OrderMapper.ToSubOrderResponse(sub);
    }
}

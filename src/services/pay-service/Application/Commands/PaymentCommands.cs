using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Security;
using PayService.Domain.Entities;
using PayService.DTOs;
using PayService.Infrastructure;
using PayService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PayService.Application.Commands;

/// <summary>创建支付单命令</summary>
public sealed record CreatePaymentCommand(Guid OrderId, decimal Amount) : ICommand<PaymentResponse>;

/// <summary>创建支付单命令处理器</summary>
public sealed class CreatePaymentCommandHandler(
    PayDbContext db,
    ICurrentUser currentUser) : ICommandHandler<CreatePaymentCommand, PaymentResponse>
{
    /// <inheritdoc />
    public async Task<PaymentResponse> HandleAsync(CreatePaymentCommand command, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty)
            throw new DomainException("请先登录", "UNAUTHENTICATED");

        // 同一订单未完成支付的支付单只能有一笔
        var pendingExists = await db.Payments.AnyAsync(
            p => p.OrderId == command.OrderId && p.Status == PayService.Domain.Enums.PaymentStatus.Pending, ct);
        if (pendingExists)
            throw new DomainException("该订单已有待支付支付单", "DUPLICATE_PAYMENT");

        var payment = new Payment(
            GeneratePayNo(), command.OrderId, currentUser.UserId, command.Amount, "simulate");

        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);
        return PaymentMapper.ToResponse(payment);
    }

    private static string GeneratePayNo()
        => $"PAY{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
}

/// <summary>模拟支付命令 — 模拟渠道支付成功，并通知 order-service 确认订单已支付</summary>
public sealed record SimulatePayCommand(Guid PaymentId) : ICommand<PaymentResponse>;

/// <summary>模拟支付命令处理器</summary>
public sealed class SimulatePayCommandHandler(
    PayDbContext db,
    ICurrentUser currentUser,
    OrderServiceClient orderClient,
    TimeProvider timeProvider,
    ILogger<SimulatePayCommandHandler> logger) : ICommandHandler<SimulatePayCommand, PaymentResponse>
{
    /// <inheritdoc />
    public async Task<PaymentResponse> HandleAsync(SimulatePayCommand command, CancellationToken ct = default)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == command.PaymentId, ct)
            ?? throw new NotFoundException("支付单", command.PaymentId);

        if (payment.BuyerUserId != currentUser.UserId)
            throw new DomainException("无权操作该支付单", "FORBIDDEN");

        // 模拟第三方渠道支付成功
        payment.MarkSuccess(timeProvider);

        // 跨服务通知订单已支付（失败不阻塞支付状态，记录日志供补偿）
        try
        {
            var result = await orderClient.ConfirmPaidAsync(payment.OrderId, ct);
            if (!result.IsSuccess)
                logger.LogWarning("订单支付确认通知失败 OrderId={OrderId} Error={Error}", payment.OrderId, result.Error);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "订单支付确认通知异常 OrderId={OrderId}", payment.OrderId);
        }

        await db.SaveChangesAsync(ct);
        return PaymentMapper.ToResponse(payment);
    }
}

/// <summary>退款命令（支付成功后）</summary>
public sealed record RefundCommand(Guid PaymentId) : ICommand<PaymentResponse>;

/// <summary>退款命令处理器</summary>
public sealed class RefundCommandHandler(
    PayDbContext db,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<RefundCommand, PaymentResponse>
{
    /// <inheritdoc />
    public async Task<PaymentResponse> HandleAsync(RefundCommand command, CancellationToken ct = default)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == command.PaymentId, ct)
            ?? throw new NotFoundException("支付单", command.PaymentId);

        if (payment.BuyerUserId != currentUser.UserId)
            throw new DomainException("无权操作该支付单", "FORBIDDEN");

        payment.MarkRefunded(timeProvider);
        await db.SaveChangesAsync(ct);
        return PaymentMapper.ToResponse(payment);
    }
}

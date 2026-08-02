using PayService.Domain.Entities;
using PayService.DTOs;

namespace PayService.Application;

/// <summary>
/// 实体 → DTO 映射。
/// </summary>
public static class PaymentMapper
{
    /// <summary>支付单实体转响应 DTO</summary>
    /// <param name="payment">支付单实体</param>
    /// <returns>支付单响应</returns>
    public static PaymentResponse ToResponse(Payment payment) => new()
    {
        Id = payment.Id,
        PayNo = payment.PayNo,
        OrderId = payment.OrderId,
        BuyerUserId = payment.BuyerUserId,
        Amount = payment.Amount,
        Channel = payment.Channel,
        Status = payment.Status,
        PaidAt = payment.PaidAt,
        RefundedAt = payment.RefundedAt,
        FailReason = payment.FailReason,
        CreatedAt = payment.CreatedAt,
    };
}

using BuildingBlocks.Core.Entities;
using PayService.Domain.Enums;

namespace PayService.Domain.Entities;

/// <summary>
/// 支付单实体 — 一次支付请求（含状态机 Pending → Success/Failed → Refunded）。
/// 状态流转内聚在实体方法（充血模型）。
/// </summary>
public sealed class Payment : Entity
{
    private Payment() { } // EF Core

    /// <summary>创建支付单（初始 Pending）</summary>
    /// <param name="payNo">业务支付单号</param>
    /// <param name="orderId">关联订单 ID</param>
    /// <param name="buyerUserId">买家用户 ID</param>
    /// <param name="amount">支付金额（元）</param>
    /// <param name="channel">支付渠道（当前模拟：simulate）</param>
    public Payment(string payNo, Guid orderId, Guid buyerUserId, decimal amount, string channel = "simulate")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payNo);
        if (amount <= 0)
            throw new ArgumentException("支付金额必须大于 0", nameof(amount));

        PayNo = payNo;
        OrderId = orderId;
        BuyerUserId = buyerUserId;
        Amount = amount;
        Channel = channel;
        Status = PaymentStatus.Pending;
    }

    /// <summary>业务支付单号（PAY + 时间戳 + 随机）</summary>
    public string PayNo { get; private set; } = null!;

    /// <summary>关联订单 ID（order-service）</summary>
    public Guid OrderId { get; private set; }

    /// <summary>买家用户 ID</summary>
    public Guid BuyerUserId { get; private set; }

    /// <summary>支付金额（元）</summary>
    public decimal Amount { get; private set; }

    /// <summary>支付渠道（当前 simulate）</summary>
    public string Channel { get; private set; } = null!;

    /// <summary>支付状态（Pending/Success/Failed/Refunded）</summary>
    public PaymentStatus Status { get; private set; }

    /// <summary>支付成功时间</summary>
    public DateTime? PaidAt { get; private set; }

    /// <summary>退款时间</summary>
    public DateTime? RefundedAt { get; private set; }

    /// <summary>最近一次失败原因</summary>
    public string? FailReason { get; private set; }

    /// <summary>是否可发起支付（仅待支付）</summary>
    public bool CanPay => Status == PaymentStatus.Pending;

    /// <summary>是否可退款（仅支付成功后）</summary>
    public bool CanRefund => Status == PaymentStatus.Success;

    /// <summary>标记支付成功</summary>
    /// <param name="timeProvider">时间提供器</param>
    public void MarkSuccess(TimeProvider timeProvider)
    {
        if (!CanPay)
            throw new InvalidOperationException($"当前状态不允许支付（{Status}）");

        Status = PaymentStatus.Success;
        PaidAt = timeProvider.GetUtcNow().UtcDateTime;
        FailReason = null;
    }

    /// <summary>标记支付失败</summary>
    /// <param name="reason">失败原因</param>
    public void MarkFailed(string reason)
    {
        if (!CanPay)
            throw new InvalidOperationException($"当前状态不允许标记失败（{Status}）");

        Status = PaymentStatus.Failed;
        FailReason = reason;
    }

    /// <summary>退款</summary>
    /// <param name="timeProvider">时间提供器</param>
    public void MarkRefunded(TimeProvider timeProvider)
    {
        if (!CanRefund)
            throw new InvalidOperationException($"当前状态不允许退款（{Status}）");

        Status = PaymentStatus.Refunded;
        RefundedAt = timeProvider.GetUtcNow().UtcDateTime;
    }
}

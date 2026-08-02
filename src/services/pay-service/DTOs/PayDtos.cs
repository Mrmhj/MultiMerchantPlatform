using System.ComponentModel.DataAnnotations;
using PayService.Domain.Enums;

namespace PayService.DTOs;

/// <summary>创建支付单请求</summary>
public sealed record CreatePaymentRequest
{
    /// <summary>关联订单 ID（order-service）</summary>
    [Required]
    public Guid OrderId { get; init; }

    /// <summary>支付金额（元，与订单金额一致）</summary>
    [Range(0.01, 99999999)]
    public decimal Amount { get; init; }
}

/// <summary>支付单响应</summary>
public sealed record PaymentResponse
{
    /// <summary>支付单 ID</summary>
    public Guid Id { get; init; }

    /// <summary>业务支付单号</summary>
    public required string PayNo { get; init; }

    /// <summary>关联订单 ID</summary>
    public Guid OrderId { get; init; }

    /// <summary>买家用户 ID</summary>
    public Guid BuyerUserId { get; init; }

    /// <summary>支付金额（元）</summary>
    public decimal Amount { get; init; }

    /// <summary>支付渠道</summary>
    public required string Channel { get; init; }

    /// <summary>支付状态（1待支付 2成功 3失败 4已退款）</summary>
    public PaymentStatus Status { get; init; }

    /// <summary>支付成功时间</summary>
    public DateTime? PaidAt { get; init; }

    /// <summary>退款时间</summary>
    public DateTime? RefundedAt { get; init; }

    /// <summary>最近一次失败原因</summary>
    public string? FailReason { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

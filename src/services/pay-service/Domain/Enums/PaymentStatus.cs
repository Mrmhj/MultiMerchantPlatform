namespace PayService.Domain.Enums;

/// <summary>
/// 支付单状态。
/// </summary>
public enum PaymentStatus
{
    /// <summary>待支付（已创建支付单）</summary>
    Pending = 1,

    /// <summary>支付成功</summary>
    Success = 2,

    /// <summary>支付失败</summary>
    Failed = 3,

    /// <summary>已退款</summary>
    Refunded = 4
}

namespace SettlementService.Domain.Enums;

/// <summary>
/// 结算单状态（Pending 待结算 → Settled 已结算 → Paid 已打款）。
/// </summary>
public enum SettlementStatus
{
    /// <summary>待结算（金额已生成，等待平台确认）</summary>
    Pending = 1,

    /// <summary>已结算（平台确认金额，等待打款）</summary>
    Settled = 2,

    /// <summary>已打款（平台完成打款）</summary>
    Paid = 3
}

namespace PromotionService.Domain.Enums;

/// <summary>
/// 优惠券类型 — 当前仅支持满减券，后续可扩展折扣券/包邮券。
/// </summary>
public enum CouponType
{
    /// <summary>满减券（满 ThresholdAmount 减 DiscountAmount）</summary>
    FullReduction = 1,
}

/// <summary>
/// 优惠券模板状态。
/// </summary>
public enum CouponStatus
{
    /// <summary>启用（可领取）</summary>
    Active = 1,

    /// <summary>停用（不可再领取，已领用不受影响）</summary>
    Inactive = 2,
}

/// <summary>
/// 用户优惠券状态 — 状态机：Unused → Used。
/// </summary>
public enum UserCouponStatus
{
    /// <summary>未使用（可核销）</summary>
    Unused = 1,

    /// <summary>已使用（核销后不可回退）</summary>
    Used = 2,

    /// <summary>已过期（查询时按有效期推导展示）</summary>
    Expired = 3,
}

/// <summary>
/// 满减活动状态 — 状态机：Draft ⇄ Active → Ended。
/// </summary>
public enum ActivityStatus
{
    /// <summary>草稿（未启用）</summary>
    Draft = 1,

    /// <summary>进行中（时间窗口内且已启用）</summary>
    Active = 2,

    /// <summary>已结束（活动过期或手动收尾）</summary>
    Ended = 3,
}

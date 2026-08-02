namespace PromotionService.Domain.Enums;

/// <summary>
/// 秒杀活动状态 — 状态机：Draft ⇄ Active → Ended。
/// </summary>
public enum SeckillStatus
{
    /// <summary>草稿（未启用，可编辑）</summary>
    Draft = 1,

    /// <summary>进行中（时间窗口内且已启用，可抢购）</summary>
    Active = 2,

    /// <summary>已结束（活动过期或手动收尾）</summary>
    Ended = 3,
}

/// <summary>
/// 秒杀记录状态 — 状态机：Pending → Ordered；超时未支付 → Expired。
/// </summary>
public enum SeckillRecordStatus
{
    /// <summary>已预扣库存，等待异步创建订单</summary>
    Pending = 1,

    /// <summary>订单已创建（order-service 回调回填订单号）</summary>
    Ordered = 2,

    /// <summary>超时未支付/下单失败，库存已回补</summary>
    Expired = 3,
}

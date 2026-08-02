namespace StockService.Domain.Enums;

/// <summary>
/// 库存流水类型。
/// </summary>
public enum StockTransactionType
{
    /// <summary>创建/设置初始库存</summary>
    Create = 1,

    /// <summary>预占（下单预留）</summary>
    Reserve = 2,

    /// <summary>确认扣减（支付后正式扣减）</summary>
    Confirm = 3,

    /// <summary>释放预占（取消回滚）</summary>
    Release = 4,

    /// <summary>补货入库</summary>
    Increase = 5
}

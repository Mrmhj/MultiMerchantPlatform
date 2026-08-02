namespace OrderService.Domain.Enums;

/// <summary>
/// 订单状态（主订单，买家维度）。
/// </summary>
public enum OrderStatus
{
    /// <summary>待付款</summary>
    Pending = 1,

    /// <summary>已付款（支付成功）</summary>
    Paid = 2,

    /// <summary>已完成（全部子单完成）</summary>
    Completed = 3,

    /// <summary>已取消</summary>
    Cancelled = 4
}

/// <summary>
/// 子订单状态（拆单后按商户维度）。
/// </summary>
public enum SubOrderStatus
{
    /// <summary>待付款</summary>
    Pending = 1,

    /// <summary>已付款</summary>
    Paid = 2,

    /// <summary>已发货（商户操作）</summary>
    Shipped = 3,

    /// <summary>已完成（买家确认/自动确认）</summary>
    Completed = 4,

    /// <summary>已取消</summary>
    Cancelled = 5
}

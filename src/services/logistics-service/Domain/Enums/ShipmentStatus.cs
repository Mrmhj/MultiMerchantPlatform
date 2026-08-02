namespace LogisticsService.Domain.Enums;

/// <summary>
/// 运单状态（物流轨迹推进状态机：Created → InTransit → OutForDelivery → Signed，任意状态可转 Exception）。
/// </summary>
public enum ShipmentStatus
{
    /// <summary>待揽收（商家已发货，物流公司未揽件）</summary>
    Created = 1,

    /// <summary>运输中</summary>
    InTransit = 2,

    /// <summary>派送中</summary>
    OutForDelivery = 3,

    /// <summary>已签收</summary>
    Signed = 4,

    /// <summary>异常（滞留/拒收/退回等）</summary>
    Exception = 5
}

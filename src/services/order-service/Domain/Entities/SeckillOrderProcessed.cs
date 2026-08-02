using BuildingBlocks.Core.Entities;

namespace OrderService.Domain.Entities;

/// <summary>
/// 秒杀订单处理幂等记录 — 键为秒杀记录 ID，保证一条秒杀记录只创建一个订单（防消息重投重复下单）。
/// </summary>
public sealed class SeckillOrderProcessed : Entity
{
    private SeckillOrderProcessed() { } // EF Core

    /// <summary>创建幂等记录</summary>
    /// <param name="seckillRecordId">秒杀记录 ID（幂等键）</param>
    /// <param name="orderId">已创建的订单 ID</param>
    /// <param name="orderNo">已创建的订单号</param>
    public SeckillOrderProcessed(Guid seckillRecordId, Guid orderId, string orderNo)
    {
        SeckillRecordId = seckillRecordId;
        OrderId = orderId;
        OrderNo = orderNo;
    }

    /// <summary>秒杀记录 ID（幂等键，唯一）</summary>
    public Guid SeckillRecordId { get; private set; }

    /// <summary>已创建的订单 ID</summary>
    public Guid OrderId { get; private set; }

    /// <summary>已创建的订单号</summary>
    public string OrderNo { get; private set; } = null!;
}

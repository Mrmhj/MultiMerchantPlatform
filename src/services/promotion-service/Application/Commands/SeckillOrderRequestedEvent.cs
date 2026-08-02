using BuildingBlocks.Core.Events;

namespace PromotionService.Application.Commands;

/// <summary>
/// 秒杀下单请求集成事件 — 抢购成功后由 promotion-service 发布，
/// order-service 订阅后异步创建秒杀订单（消费端按 EventName="SeckillOrderRequested" 匹配）。
/// </summary>
public sealed record SeckillOrderRequestedEvent : IntegrationEvent
{
    /// <summary>秒杀记录 ID（幂等键）</summary>
    public required Guid RecordId { get; init; }

    /// <summary>秒杀活动 ID</summary>
    public required Guid ActivityId { get; init; }

    /// <summary>商户 ID（快照）</summary>
    public required Guid MerchantId { get; init; }

    /// <summary>商户名称（快照）</summary>
    public required string MerchantName { get; init; }

    /// <summary>买家用户 ID</summary>
    public required Guid UserId { get; init; }

    /// <summary>商品 ID（快照）</summary>
    public required Guid ProductId { get; init; }

    /// <summary>商品名称（快照）</summary>
    public required string ProductName { get; init; }

    /// <summary>SKU ID（快照）</summary>
    public required Guid SkuId { get; init; }

    /// <summary>SKU 编码（快照）</summary>
    public required string SkuCode { get; init; }

    /// <summary>规格（快照）</summary>
    public required string Spec { get; init; }

    /// <summary>秒杀价（快照）</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>购买数量</summary>
    public required int Quantity { get; init; }

    /// <summary>订单支付截止（超时未支付回滚库存）</summary>
    public required DateTime ExpireAt { get; init; }
}

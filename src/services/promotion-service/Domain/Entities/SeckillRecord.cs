using BuildingBlocks.Core.Entities;
using PromotionService.Domain.Enums;

namespace PromotionService.Domain.Entities;

/// <summary>
/// 秒杀记录 — 买家维度（UserId 隔离），记录一次抢购成功（库存已预扣）。
/// 状态机：Pending（已预扣待下单）→ Ordered（订单已创建）；超时未支付 → Expired（库存回补）。
/// </summary>
public sealed class SeckillRecord : Entity
{
    private SeckillRecord() { } // EF Core

    /// <summary>创建秒杀记录（抢购成功后，初始 Pending）</summary>
    /// <param name="activityId">秒杀活动 ID</param>
    /// <param name="merchantId">商户 ID（快照）</param>
    /// <param name="merchantName">商户名称（快照）</param>
    /// <param name="userId">买家用户 ID</param>
    /// <param name="productId">商品 ID（快照）</param>
    /// <param name="productName">商品名称（快照）</param>
    /// <param name="skuId">SKU ID（快照）</param>
    /// <param name="skuCode">SKU 编码（快照）</param>
    /// <param name="spec">规格（快照）</param>
    /// <param name="unitPrice">秒杀价（快照）</param>
    /// <param name="quantity">购买数量</param>
    /// <param name="expireAt">订单支付截止（超时未支付回滚库存）</param>
    public SeckillRecord(
        Guid activityId, Guid merchantId, string merchantName, Guid userId,
        Guid productId, string productName, Guid skuId, string skuCode, string spec,
        decimal unitPrice, int quantity, DateTime expireAt)
    {
        ActivityId = activityId;
        MerchantId = merchantId;
        MerchantName = merchantName;
        UserId = userId;
        ProductId = productId;
        ProductName = productName;
        SkuId = skuId;
        SkuCode = skuCode;
        Spec = spec;
        UnitPrice = unitPrice;
        Quantity = quantity;
        ExpireAt = expireAt;
        Status = SeckillRecordStatus.Pending;
    }

    /// <summary>秒杀活动 ID</summary>
    public Guid ActivityId { get; private set; }

    /// <summary>商户 ID（快照）</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>商户名称（快照）</summary>
    public string MerchantName { get; private set; } = string.Empty;

    /// <summary>买家用户 ID</summary>
    public Guid UserId { get; private set; }

    /// <summary>商品 ID（快照）</summary>
    public Guid ProductId { get; private set; }

    /// <summary>商品名称（快照）</summary>
    public string ProductName { get; private set; } = string.Empty;

    /// <summary>SKU ID（快照）</summary>
    public Guid SkuId { get; private set; }

    /// <summary>SKU 编码（快照）</summary>
    public string SkuCode { get; private set; } = string.Empty;

    /// <summary>规格（快照）</summary>
    public string Spec { get; private set; } = string.Empty;

    /// <summary>秒杀价（快照）</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>购买数量</summary>
    public int Quantity { get; private set; }

    /// <summary>订单支付截止（超时未支付回滚库存）</summary>
    public DateTime ExpireAt { get; private set; }

    /// <summary>关联订单 ID（异步下单成功后回填）</summary>
    public Guid? OrderId { get; private set; }

    /// <summary>关联订单号（异步下单成功后回填）</summary>
    public string? OrderNo { get; private set; }

    /// <summary>状态（Pending/Ordered/Expired）</summary>
    public SeckillRecordStatus Status { get; private set; }

    /// <summary>标记订单已创建（order-service 异步下单成功后回调）</summary>
    /// <param name="orderId">订单 ID</param>
    /// <param name="orderNo">订单号</param>
    public void MarkOrdered(Guid orderId, string orderNo)
    {
        if (Status != SeckillRecordStatus.Pending)
            return; // 幂等：仅 Pending 可流转
        OrderId = orderId;
        OrderNo = orderNo;
        Status = SeckillRecordStatus.Ordered;
    }

    /// <summary>标记过期（超时未支付，库存由调用方回补 Redis）</summary>
    public void MarkExpired()
    {
        if (Status != SeckillRecordStatus.Pending)
            return; // 幂等：仅 Pending 可流转
        Status = SeckillRecordStatus.Expired;
    }
}

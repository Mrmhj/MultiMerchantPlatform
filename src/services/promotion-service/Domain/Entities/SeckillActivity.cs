using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;
using PromotionService.Domain.Enums;

namespace PromotionService.Domain.Entities;

/// <summary>
/// 秒杀活动 — 商户维度（MultiTenantEntity），绑定单一 SKU。
/// 状态机：Draft ⇄ Active → Ended；启用时将库存预热到 Redis（缓存预扣），停用/结束回收。
/// </summary>
public sealed class SeckillActivity : MultiTenantEntity
{
    private SeckillActivity() { } // EF Core

    /// <summary>创建秒杀活动（初始 Draft）</summary>
    /// <param name="merchantId">所属商户 ID</param>
    /// <param name="merchantName">商户名称（快照）</param>
    /// <param name="name">活动名称</param>
    /// <param name="productId">商品 ID</param>
    /// <param name="productName">商品名称（快照）</param>
    /// <param name="skuId">SKU ID</param>
    /// <param name="skuCode">SKU 编码（快照）</param>
    /// <param name="spec">规格（快照）</param>
    /// <param name="seckillPrice">秒杀价</param>
    /// <param name="totalStock">秒杀总库存</param>
    /// <param name="limitPerUser">每人限购数量</param>
    /// <param name="startTime">开始时间（UTC）</param>
    /// <param name="endTime">结束时间（UTC）</param>
    [SetsRequiredMembers]
    public SeckillActivity(
        Guid merchantId, string merchantName, string name,
        Guid productId, string productName,
        Guid skuId, string skuCode, string spec,
        decimal seckillPrice, int totalStock, int limitPerUser,
        DateTime startTime, DateTime endTime)
    {
        MerchantId = merchantId;
        ChangeMerchantName(merchantName);
        ChangeName(name);
        ChangeProduct(productId, productName, skuId, skuCode, spec);
        ChangePriceAndStock(seckillPrice, totalStock, limitPerUser);
        ChangePeriod(startTime, endTime);
    }

    /// <summary>商户名称（快照）</summary>
    public string MerchantName { get; private set; } = string.Empty;

    /// <summary>修改商户名称（快照）</summary>
    /// <param name="merchantName">商户名称（1-100 字）</param>
    public void ChangeMerchantName(string merchantName)
    {
        if (string.IsNullOrWhiteSpace(merchantName) || merchantName.Length > 100)
            throw new DomainException("商户名称需在 1-100 字之间", "INVALID_MERCHANT_NAME");
        MerchantName = merchantName.Trim();
    }

    /// <summary>活动名称</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>商品 ID</summary>
    public Guid ProductId { get; private set; }

    /// <summary>商品名称（快照）</summary>
    public string ProductName { get; private set; } = string.Empty;

    /// <summary>SKU ID</summary>
    public Guid SkuId { get; private set; }

    /// <summary>SKU 编码（快照）</summary>
    public string SkuCode { get; private set; } = string.Empty;

    /// <summary>规格（快照）</summary>
    public string Spec { get; private set; } = string.Empty;

    /// <summary>秒杀价</summary>
    public decimal SeckillPrice { get; private set; }

    /// <summary>秒杀总库存</summary>
    public int TotalStock { get; private set; }

    /// <summary>每人限购数量</summary>
    public int LimitPerUser { get; private set; }

    /// <summary>开始时间（UTC）</summary>
    public DateTime StartTime { get; private set; }

    /// <summary>结束时间（UTC）</summary>
    public DateTime EndTime { get; private set; }

    /// <summary>状态（Draft/Active/Ended）</summary>
    public SeckillStatus Status { get; private set; } = SeckillStatus.Draft;

    /// <summary>修改活动名称</summary>
    /// <param name="name">新名称（1-100 字）</param>
    public void ChangeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            throw new DomainException("秒杀活动名称需在 1-100 字之间", "INVALID_NAME");
        Name = name.Trim();
    }

    /// <summary>修改商品信息（SKU 维度）</summary>
    /// <param name="productId">商品 ID</param>
    /// <param name="productName">商品名称</param>
    /// <param name="skuId">SKU ID</param>
    /// <param name="skuCode">SKU 编码</param>
    /// <param name="spec">规格</param>
    public void ChangeProduct(Guid productId, string productName, Guid skuId, string skuCode, string spec)
    {
        if (productId == Guid.Empty || skuId == Guid.Empty)
            throw new DomainException("商品与 SKU 不能为空", "INVALID_PRODUCT");
        if (string.IsNullOrWhiteSpace(productName) || productName.Length > 200)
            throw new DomainException("商品名称需在 1-200 字之间", "INVALID_PRODUCT_NAME");
        if (string.IsNullOrWhiteSpace(skuCode) || skuCode.Length > 50)
            throw new DomainException("SKU 编码需在 1-50 字之间", "INVALID_SKU_CODE");

        ProductId = productId;
        ProductName = productName.Trim();
        SkuId = skuId;
        SkuCode = skuCode.Trim();
        Spec = spec?.Trim() ?? string.Empty;
    }

    /// <summary>修改秒杀价/库存/限购</summary>
    /// <param name="seckillPrice">秒杀价（&gt; 0）</param>
    /// <param name="totalStock">总库存（&gt; 0）</param>
    /// <param name="limitPerUser">每人限购（1-999）</param>
    public void ChangePriceAndStock(decimal seckillPrice, int totalStock, int limitPerUser)
    {
        if (seckillPrice <= 0)
            throw new DomainException("秒杀价必须大于 0", "INVALID_PRICE");
        if (totalStock <= 0 || totalStock > 999999)
            throw new DomainException("秒杀库存需在 1-999999 之间", "INVALID_STOCK");
        if (limitPerUser <= 0 || limitPerUser > 999)
            throw new DomainException("每人限购需在 1-999 之间", "INVALID_LIMIT");

        SeckillPrice = seckillPrice;
        TotalStock = totalStock;
        LimitPerUser = limitPerUser;
    }

    /// <summary>修改活动时间窗口</summary>
    /// <param name="startTime">开始时间（UTC）</param>
    /// <param name="endTime">结束时间（UTC）</param>
    public void ChangePeriod(DateTime startTime, DateTime endTime)
    {
        if (endTime <= startTime)
            throw new DomainException("结束时间必须晚于开始时间", "INVALID_PERIOD");
        StartTime = startTime;
        EndTime = endTime;
    }

    /// <summary>指定时刻是否处于生效窗口内（时间维度判断，与 Status 无关）</summary>
    /// <param name="now">当前时间（UTC）</param>
    /// <returns>是否在窗口内</returns>
    public bool InPeriodAt(DateTime now) => now >= StartTime && now <= EndTime;

    /// <summary>启用活动（校验时间窗口有效）</summary>
    /// <param name="now">当前时间（UTC）</param>
    public void Activate(DateTime now)
    {
        if (now > EndTime)
            throw new DomainException("活动已过结束时间，无法启用", "ACTIVITY_ENDED");
        Status = SeckillStatus.Active;
    }

    /// <summary>停用活动（进行中活动手动终止 → 回 Draft，重新启用走 Activate）</summary>
    public void Disable()
    {
        if (Status == SeckillStatus.Ended)
            throw new DomainException("活动已结束，无法停用", "ACTIVITY_ENDED");
        if (Status != SeckillStatus.Active)
            throw new DomainException("活动未处于启用状态", "ACTIVITY_NOT_ACTIVE");
        Status = SeckillStatus.Draft;
    }

    /// <summary>到期收尾（活动已过结束时间时由查询/后台推导为 Ended）</summary>
    /// <param name="now">当前时间（UTC）</param>
    public void EndIfExpired(DateTime now)
    {
        if (Status == SeckillStatus.Active && now > EndTime)
            Status = SeckillStatus.Ended;
    }
}

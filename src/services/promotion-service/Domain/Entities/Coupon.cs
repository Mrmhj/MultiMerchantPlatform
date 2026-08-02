using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;
using PromotionService.Domain.Enums;

namespace PromotionService.Domain.Entities;

/// <summary>
/// 优惠券模板 — 商户维度（MultiTenantEntity），商户创建/维护，买家按模板领取。
/// 仅支持满减券（满 ThresholdAmount 减 DiscountAmount），有效期窗口内可领。
/// </summary>
public sealed class Coupon : MultiTenantEntity
{
    private Coupon() { } // EF Core

    /// <summary>创建优惠券模板</summary>
    /// <param name="merchantId">所属商户 ID</param>
    /// <param name="name">券名称</param>
    /// <param name="thresholdAmount">满 X 元可用</param>
    /// <param name="discountAmount">减 Y 元</param>
    /// <param name="totalQuantity">发行总量（0 表示不限量）</param>
    /// <param name="limitPerUser">每人限领数量（1-99）</param>
    /// <param name="validFrom">领取/使用有效期开始</param>
    /// <param name="validUntil">领取/使用有效期截止</param>
    [SetsRequiredMembers]
    public Coupon(Guid merchantId, string name, decimal thresholdAmount, decimal discountAmount,
        int totalQuantity, int limitPerUser, DateTime validFrom, DateTime validUntil)
    {
        MerchantId = merchantId;
        ChangeName(name);
        ChangeAmount(thresholdAmount, discountAmount);
        TotalQuantity = totalQuantity;
        LimitPerUser = limitPerUser is >= 1 and <= 99
            ? limitPerUser
            : throw new DomainException("每人限领需在 1-99 之间", "INVALID_LIMIT_PER_USER");
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        if (ValidUntil <= ValidFrom)
            throw new DomainException("有效期截止必须晚于开始时间", "INVALID_VALID_PERIOD");
    }

    /// <summary>券名称</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>券类型（当前仅满减）</summary>
    public CouponType Type { get; private set; } = CouponType.FullReduction;

    /// <summary>满 X 元可用（0 表示无门槛）</summary>
    public decimal ThresholdAmount { get; private set; }

    /// <summary>减 Y 元</summary>
    public decimal DiscountAmount { get; private set; }

    /// <summary>发行总量（0 表示不限量）</summary>
    public int TotalQuantity { get; private set; }

    /// <summary>已领取数量</summary>
    public int ClaimedCount { get; private set; }

    /// <summary>每人限领数量</summary>
    public int LimitPerUser { get; private set; }

    /// <summary>领取/使用有效期开始</summary>
    public DateTime ValidFrom { get; private set; }

    /// <summary>领取/使用有效期截止</summary>
    public DateTime ValidUntil { get; private set; }

    /// <summary>模板状态（启用/停用）</summary>
    public CouponStatus Status { get; private set; } = CouponStatus.Active;

    /// <summary>修改券名称</summary>
    /// <param name="name">新名称（1-50 字）</param>
    public void ChangeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 50)
            throw new DomainException("券名称需在 1-50 字之间", "INVALID_COUPON_NAME");
        Name = name.Trim();
    }

    /// <summary>修改满减金额（满 X 减 Y，Y 不得大于 X）</summary>
    /// <param name="thresholdAmount">满 X 元</param>
    /// <param name="discountAmount">减 Y 元</param>
    public void ChangeAmount(decimal thresholdAmount, decimal discountAmount)
    {
        if (thresholdAmount < 0 || discountAmount <= 0)
            throw new DomainException("门槛需 >= 0 且优惠金额必须 > 0", "INVALID_AMOUNT");
        if (thresholdAmount > 0 && discountAmount > thresholdAmount)
            throw new DomainException("优惠金额不能大于使用门槛", "DISCOUNT_EXCEEDS_THRESHOLD");
        ThresholdAmount = thresholdAmount;
        DiscountAmount = discountAmount;
    }

    /// <summary>当前时刻是否可领取（启用 + 在有效期窗口内 + 未领完）</summary>
    /// <param name="now">当前时间（UTC）</param>
    /// <returns>是否可领取</returns>
    public bool IsClaimableAt(DateTime now)
        => Status == CouponStatus.Active
           && now >= ValidFrom && now <= ValidUntil
           && (TotalQuantity <= 0 || ClaimedCount < TotalQuantity);

    /// <summary>领取一张（校验可领状态，超出总量拒绝）</summary>
    /// <param name="now">当前时间（UTC）</param>
    public void ClaimOne(DateTime now)
    {
        if (!IsClaimableAt(now))
            throw new DomainException("优惠券不可领取（未启用/未到有效期/已领完）", "COUPON_NOT_CLAIMABLE");
        ClaimedCount++;
    }

    /// <summary>停用（已领用不受影响）</summary>
    public void Disable()
    {
        if (Status == CouponStatus.Inactive)
            throw new DomainException("优惠券已处于停用状态", "COUPON_ALREADY_INACTIVE");
        Status = CouponStatus.Inactive;
    }

    /// <summary>重新启用</summary>
    public void Enable()
    {
        if (Status == CouponStatus.Active)
            throw new DomainException("优惠券已处于启用状态", "COUPON_ALREADY_ACTIVE");
        Status = CouponStatus.Active;
    }
}

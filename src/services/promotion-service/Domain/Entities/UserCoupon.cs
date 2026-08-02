using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;
using PromotionService.Domain.Enums;

namespace PromotionService.Domain.Entities;

/// <summary>
/// 用户优惠券 — 买家维度隔离（UserId 归属），记录领取的券及核销状态。
/// 状态机：Unused → Used（核销后不可回退）。
/// </summary>
public sealed class UserCoupon : Entity
{
    private UserCoupon() { } // EF Core

    /// <summary>领取优惠券</summary>
    /// <param name="userId">买家用户 ID</param>
    /// <param name="coupon">优惠券模板（需已调用 ClaimOne 增加领取数）</param>
    /// <param name="now">领取时间（UTC）</param>
    public UserCoupon(Guid userId, Coupon coupon, DateTime now)
    {
        UserId = userId;
        CouponId = coupon.Id;
        MerchantId = coupon.MerchantId;
        Name = coupon.Name;
        Type = coupon.Type;
        ThresholdAmount = coupon.ThresholdAmount;
        DiscountAmount = coupon.DiscountAmount;
        ValidFrom = coupon.ValidFrom;
        ValidUntil = coupon.ValidUntil;
        ClaimedAt = now;
    }

    /// <summary>买家用户 ID（隔离维度）</summary>
    public Guid UserId { get; private set; }

    /// <summary>优惠券模板 ID</summary>
    public Guid CouponId { get; private set; }

    /// <summary>所属商户 ID（冗余，便于商户侧查询与核销校验）</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>券名称（领取时快照）</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>券类型（领取时快照）</summary>
    public CouponType Type { get; private set; }

    /// <summary>满 X 元可用（快照）</summary>
    public decimal ThresholdAmount { get; private set; }

    /// <summary>减 Y 元（快照）</summary>
    public decimal DiscountAmount { get; private set; }

    /// <summary>有效期开始（快照）</summary>
    public DateTime ValidFrom { get; private set; }

    /// <summary>有效期截止（快照）</summary>
    public DateTime ValidUntil { get; private set; }

    /// <summary>领取时间</summary>
    public DateTime ClaimedAt { get; private set; }

    /// <summary>使用时间（核销后写入）</summary>
    public DateTime? UsedAt { get; private set; }

    /// <summary>状态（Unused/Used，Expired 由查询按有效期推导）</summary>
    public UserCouponStatus Status { get; private set; } = UserCouponStatus.Unused;

    /// <summary>核销为已使用（幂等：已核销再次调用不报错）</summary>
    /// <param name="now">核销时间（UTC）</param>
    public void MarkUsed(DateTime now)
    {
        if (Status == UserCouponStatus.Used)
            return;
        if (Status == UserCouponStatus.Expired || now > ValidUntil)
            throw new DomainException("优惠券已过期，无法核销", "COUPON_EXPIRED");
        if (now < ValidFrom)
            throw new DomainException("优惠券未到可用时间", "COUPON_NOT_STARTED");
        Status = UserCouponStatus.Used;
        UsedAt = now;
    }
}

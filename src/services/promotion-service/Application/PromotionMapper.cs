using PromotionService.Domain.Entities;
using PromotionService.Domain.Enums;
using PromotionService.DTOs;

namespace PromotionService.Application;

/// <summary>
/// 实体 → DTO 映射。
/// </summary>
public static class PromotionMapper
{
    /// <summary>优惠券模板实体转响应 DTO（可领状态按当前时刻计算）</summary>
    /// <param name="coupon">优惠券模板</param>
    /// <param name="now">当前时间（UTC）</param>
    /// <returns>优惠券响应</returns>
    public static CouponResponse ToCouponResponse(Coupon coupon, DateTime now) => new()
    {
        Id = coupon.Id,
        MerchantId = coupon.MerchantId,
        Name = coupon.Name,
        Type = coupon.Type,
        ThresholdAmount = coupon.ThresholdAmount,
        DiscountAmount = coupon.DiscountAmount,
        TotalQuantity = coupon.TotalQuantity,
        ClaimedCount = coupon.ClaimedCount,
        LimitPerUser = coupon.LimitPerUser,
        ValidFrom = coupon.ValidFrom,
        ValidUntil = coupon.ValidUntil,
        Status = coupon.Status,
        IsClaimable = coupon.IsClaimableAt(now),
        CreatedAt = coupon.CreatedAt,
    };

    /// <summary>用户优惠券实体转响应 DTO（过期状态按有效期推导）</summary>
    /// <param name="userCoupon">用户优惠券</param>
    /// <param name="now">当前时间（UTC）</param>
    /// <returns>用户优惠券响应</returns>
    public static UserCouponResponse ToUserCouponResponse(UserCoupon userCoupon, DateTime now)
    {
        var status = userCoupon.Status;
        if (status == UserCouponStatus.Unused && now > userCoupon.ValidUntil)
            status = UserCouponStatus.Expired;

        return new UserCouponResponse
        {
            Id = userCoupon.Id,
            MerchantId = userCoupon.MerchantId,
            Name = userCoupon.Name,
            Type = userCoupon.Type,
            ThresholdAmount = userCoupon.ThresholdAmount,
            DiscountAmount = userCoupon.DiscountAmount,
            ValidFrom = userCoupon.ValidFrom,
            ValidUntil = userCoupon.ValidUntil,
            ClaimedAt = userCoupon.ClaimedAt,
            UsedAt = userCoupon.UsedAt,
            Status = status,
        };
    }

    /// <summary>满减活动实体转响应 DTO（自动推导 Ended 终态）</summary>
    /// <param name="activity">满减活动</param>
    /// <param name="now">当前时间（UTC）</param>
    /// <returns>活动响应</returns>
    public static ActivityResponse ToActivityResponse(PromotionActivity activity, DateTime now)
    {
        activity.EndIfExpired(now);
        return new ActivityResponse
        {
            Id = activity.Id,
            MerchantId = activity.MerchantId,
            Name = activity.Name,
            Type = activity.Type,
            ThresholdAmount = activity.ThresholdAmount,
            DiscountAmount = activity.DiscountAmount,
            StartTime = activity.StartTime,
            EndTime = activity.EndTime,
            Status = activity.Status,
            CreatedAt = activity.CreatedAt,
        };
    }

    /// <summary>秒杀活动实体转响应 DTO（自动推导 Ended 终态）</summary>
    /// <param name="activity">秒杀活动</param>
    /// <param name="now">当前时间（UTC）</param>
    /// <returns>秒杀活动响应</returns>
    public static SeckillResponse ToSeckillResponse(SeckillActivity activity, DateTime now)
    {
        activity.EndIfExpired(now);
        return new SeckillResponse
        {
            Id = activity.Id,
            MerchantId = activity.MerchantId,
            Name = activity.Name,
            MerchantName = activity.MerchantName,
            ProductId = activity.ProductId,
            ProductName = activity.ProductName,
            SkuId = activity.SkuId,
            SkuCode = activity.SkuCode,
            Spec = activity.Spec,
            SeckillPrice = activity.SeckillPrice,
            TotalStock = activity.TotalStock,
            LimitPerUser = activity.LimitPerUser,
            StartTime = activity.StartTime,
            EndTime = activity.EndTime,
            Status = activity.Status,
            CreatedAt = activity.CreatedAt,
        };
    }

    /// <summary>秒杀记录实体转响应 DTO</summary>
    /// <param name="record">秒杀记录</param>
    /// <returns>秒杀记录响应</returns>
    public static SeckillRecordResponse ToSeckillRecordResponse(SeckillRecord record) => new()
    {
        Id = record.Id,
        ActivityId = record.ActivityId,
        MerchantId = record.MerchantId,
        MerchantName = record.MerchantName,
        UserId = record.UserId,
        ProductName = record.ProductName,
        SkuCode = record.SkuCode,
        Spec = record.Spec,
        UnitPrice = record.UnitPrice,
        Quantity = record.Quantity,
        ExpireAt = record.ExpireAt,
        OrderId = record.OrderId,
        OrderNo = record.OrderNo,
        Status = record.Status,
        CreatedAt = record.CreatedAt,
    };
}

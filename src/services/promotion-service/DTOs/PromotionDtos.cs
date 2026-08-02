using System.ComponentModel.DataAnnotations;
using PromotionService.Domain.Enums;

namespace PromotionService.DTOs;

/// <summary>创建优惠券请求（商户端）</summary>
public sealed record CreateCouponRequest
{
    /// <summary>券名称（1-50 字）</summary>
    [Required, StringLength(50)]
    public required string Name { get; init; }

    /// <summary>满 X 元可用（0 表示无门槛）</summary>
    [Range(0, 9999999)]
    public decimal ThresholdAmount { get; init; }

    /// <summary>减 Y 元（必须 > 0 且不大于门槛）</summary>
    [Range(0.01, 9999999)]
    public decimal DiscountAmount { get; init; }

    /// <summary>发行总量（0 表示不限量）</summary>
    [Range(0, 999999)]
    public int TotalQuantity { get; init; }

    /// <summary>每人限领数量（1-99）</summary>
    [Range(1, 99)]
    public int LimitPerUser { get; init; } = 1;

    /// <summary>领取/使用有效期开始（UTC）</summary>
    [Required]
    public DateTime ValidFrom { get; init; }

    /// <summary>领取/使用有效期截止（UTC）</summary>
    [Required]
    public DateTime ValidUntil { get; init; }
}

/// <summary>优惠券状态变更请求</summary>
public sealed record ChangeCouponStatusRequest
{
    /// <summary>目标状态：启用 true / 停用 false</summary>
    [Required]
    public bool Active { get; init; }
}

/// <summary>创建满减活动请求（商户端）</summary>
public sealed record CreateActivityRequest
{
    /// <summary>活动名称（1-100 字）</summary>
    [Required, StringLength(100)]
    public required string Name { get; init; }

    /// <summary>满 X 元（0 表示无门槛）</summary>
    [Range(0, 9999999)]
    public decimal ThresholdAmount { get; init; }

    /// <summary>减 Y 元（必须 > 0 且不大于门槛）</summary>
    [Range(0.01, 9999999)]
    public decimal DiscountAmount { get; init; }

    /// <summary>开始时间（UTC）</summary>
    [Required]
    public DateTime StartTime { get; init; }

    /// <summary>结束时间（UTC）</summary>
    [Required]
    public DateTime EndTime { get; init; }
}

/// <summary>活动状态变更请求</summary>
public sealed record ChangeActivityStatusRequest
{
    /// <summary>目标状态：启用 true / 停用 false</summary>
    [Required]
    public bool Active { get; init; }
}

/// <summary>内部核销请求（order-service 回调，X-Internal-Key）</summary>
public sealed record UseUserCouponRequest
{
    /// <summary>买家用户 ID</summary>
    [Required]
    public Guid UserId { get; init; }

    /// <summary>用户优惠券 ID（我的券条目 ID）</summary>
    [Required]
    public Guid UserCouponId { get; init; }

    /// <summary>关联订单 ID（核销记录，便于对账）</summary>
    public Guid? OrderId { get; init; }
}

/// <summary>优惠券模板响应</summary>
public sealed record CouponResponse
{
    /// <summary>优惠券模板 ID</summary>
    public Guid Id { get; init; }

    /// <summary>所属商户 ID</summary>
    public Guid MerchantId { get; init; }

    /// <summary>券名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>券类型</summary>
    public CouponType Type { get; init; }

    /// <summary>满 X 元可用</summary>
    public decimal ThresholdAmount { get; init; }

    /// <summary>减 Y 元</summary>
    public decimal DiscountAmount { get; init; }

    /// <summary>发行总量（0=不限量）</summary>
    public int TotalQuantity { get; init; }

    /// <summary>已领取数量</summary>
    public int ClaimedCount { get; init; }

    /// <summary>每人限领数量</summary>
    public int LimitPerUser { get; init; }

    /// <summary>领取/使用有效期开始</summary>
    public DateTime ValidFrom { get; init; }

    /// <summary>领取/使用有效期截止</summary>
    public DateTime ValidUntil { get; init; }

    /// <summary>模板状态</summary>
    public CouponStatus Status { get; init; }

    /// <summary>是否可领取（当前时刻）</summary>
    public bool IsClaimable { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>用户优惠券响应（我的券）</summary>
public sealed record UserCouponResponse
{
    /// <summary>用户优惠券 ID</summary>
    public Guid Id { get; init; }

    /// <summary>所属商户 ID</summary>
    public Guid MerchantId { get; init; }

    /// <summary>券名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>券类型</summary>
    public CouponType Type { get; init; }

    /// <summary>满 X 元可用</summary>
    public decimal ThresholdAmount { get; init; }

    /// <summary>减 Y 元</summary>
    public decimal DiscountAmount { get; init; }

    /// <summary>有效期开始</summary>
    public DateTime ValidFrom { get; init; }

    /// <summary>有效期截止</summary>
    public DateTime ValidUntil { get; init; }

    /// <summary>领取时间</summary>
    public DateTime ClaimedAt { get; init; }

    /// <summary>使用时间（未使用为 null）</summary>
    public DateTime? UsedAt { get; init; }

    /// <summary>状态（Unused/Used/Expired，过期由有效期推导）</summary>
    public UserCouponStatus Status { get; init; }
}

/// <summary>满减活动响应</summary>
public sealed record ActivityResponse
{
    /// <summary>活动 ID</summary>
    public Guid Id { get; init; }

    /// <summary>所属商户 ID</summary>
    public Guid MerchantId { get; init; }

    /// <summary>活动名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>活动类型</summary>
    public CouponType Type { get; init; }

    /// <summary>满 X 元</summary>
    public decimal ThresholdAmount { get; init; }

    /// <summary>减 Y 元</summary>
    public decimal DiscountAmount { get; init; }

    /// <summary>开始时间</summary>
    public DateTime StartTime { get; init; }

    /// <summary>结束时间</summary>
    public DateTime EndTime { get; init; }

    /// <summary>状态（Draft/Active/Ended）</summary>
    public ActivityStatus Status { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>优惠券核销结果（内部接口）</summary>
public sealed record UseCouponResult
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>失败原因（成功为 null）</summary>
    public string? Error { get; init; }

    /// <summary>失败码（成功为 null）</summary>
    public string? ErrorCode { get; init; }

    /// <summary>核销的优惠金额（成功时返回）</summary>
    public decimal DiscountAmount { get; init; }

    /// <summary>核销时间（成功时返回）</summary>
    public DateTime? UsedAt { get; init; }
}

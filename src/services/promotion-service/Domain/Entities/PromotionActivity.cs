using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;
using PromotionService.Domain.Enums;

namespace PromotionService.Domain.Entities;

/// <summary>
/// 满减活动 — 商户维度（MultiTenantEntity），按时间窗口自动生效。
/// 状态机：Draft → Active → Ended；手动停用可随时 Disable（恢复启用走 Activate）。
/// </summary>
public sealed class PromotionActivity : MultiTenantEntity
{
    private PromotionActivity() { } // EF Core

    /// <summary>创建满减活动（初始 Draft）</summary>
    /// <param name="merchantId">所属商户 ID</param>
    /// <param name="name">活动名称</param>
    /// <param name="thresholdAmount">满 X 元</param>
    /// <param name="discountAmount">减 Y 元</param>
    /// <param name="startTime">开始时间（UTC）</param>
    /// <param name="endTime">结束时间（UTC）</param>
    [SetsRequiredMembers]
    public PromotionActivity(Guid merchantId, string name, decimal thresholdAmount, decimal discountAmount,
        DateTime startTime, DateTime endTime)
    {
        MerchantId = merchantId;
        ChangeName(name);
        ChangeAmount(thresholdAmount, discountAmount);
        ChangePeriod(startTime, endTime);
    }

    /// <summary>活动名称</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>活动类型（当前仅满减）</summary>
    public CouponType Type { get; private set; } = CouponType.FullReduction;

    /// <summary>满 X 元（0 表示无门槛）</summary>
    public decimal ThresholdAmount { get; private set; }

    /// <summary>减 Y 元</summary>
    public decimal DiscountAmount { get; private set; }

    /// <summary>开始时间（UTC）</summary>
    public DateTime StartTime { get; private set; }

    /// <summary>结束时间（UTC）</summary>
    public DateTime EndTime { get; private set; }

    /// <summary>状态（Draft/Active/Ended）</summary>
    public ActivityStatus Status { get; private set; } = ActivityStatus.Draft;

    /// <summary>修改活动名称</summary>
    /// <param name="name">新名称（1-100 字）</param>
    public void ChangeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            throw new DomainException("活动名称需在 1-100 字之间", "INVALID_ACTIVITY_NAME");
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

    /// <summary>启用活动（校验时间窗口有效，需当前时刻在窗口内或尚未开始）</summary>
    /// <param name="now">当前时间（UTC）</param>
    public void Activate(DateTime now)
    {
        if (now > EndTime)
            throw new DomainException("活动已过结束时间，无法启用", "ACTIVITY_ENDED");
        Status = ActivityStatus.Active;
    }

    /// <summary>停用活动（进行中活动手动终止）</summary>
    public void Disable()
    {
        if (Status == ActivityStatus.Ended)
            throw new DomainException("活动已结束，无法停用", "ACTIVITY_ENDED");
        if (Status != ActivityStatus.Active)
            throw new DomainException("活动未处于启用状态", "ACTIVITY_NOT_ACTIVE");
        Status = ActivityStatus.Draft;
    }

    /// <summary>到期收尾（活动已过结束时间时由查询/后台推导为 Ended）</summary>
    /// <param name="now">当前时间（UTC）</param>
    public void EndIfExpired(DateTime now)
    {
        if (Status == ActivityStatus.Active && now > EndTime)
            Status = ActivityStatus.Ended;
    }
}

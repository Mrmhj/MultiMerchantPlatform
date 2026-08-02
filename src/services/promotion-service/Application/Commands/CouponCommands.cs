using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.MultiTenant;
using PromotionService.Domain.Entities;
using PromotionService.DTOs;
using PromotionService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PromotionService.Application.Commands;

/// <summary>创建优惠券命令（商户端）</summary>
/// <param name="Request">券信息</param>
public sealed record CreateCouponCommand(CreateCouponRequest Request) : ICommand<CouponResponse>;

/// <summary>创建优惠券命令处理器</summary>
public sealed class CreateCouponCommandHandler(
    PromotionDbContext db,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider) : ICommandHandler<CreateCouponCommand, CouponResponse>
{
    /// <inheritdoc />
    public async Task<CouponResponse> HandleAsync(CreateCouponCommand command, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var r = command.Request;
        var coupon = new Coupon(merchantId, r.Name, r.ThresholdAmount, r.DiscountAmount,
            r.TotalQuantity, r.LimitPerUser, r.ValidFrom, r.ValidUntil);
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync(ct);

        return PromotionMapper.ToCouponResponse(coupon, timeProvider.GetUtcNow().UtcDateTime);
    }
}

/// <summary>变更优惠券状态命令（启用/停用）</summary>
/// <param name="Id">优惠券模板 ID</param>
/// <param name="Active">目标状态：true 启用 / false 停用</param>
public sealed record ChangeCouponStatusCommand(Guid Id, bool Active) : ICommand<CouponResponse>;

/// <summary>变更优惠券状态命令处理器</summary>
public sealed class ChangeCouponStatusCommandHandler(
    PromotionDbContext db,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider) : ICommandHandler<ChangeCouponStatusCommand, CouponResponse>
{
    /// <inheritdoc />
    public async Task<CouponResponse> HandleAsync(ChangeCouponStatusCommand command, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var coupon = await db.Coupons.FirstOrDefaultAsync(
            c => c.Id == command.Id && c.MerchantId == merchantId, ct)
            ?? throw new NotFoundException("优惠券", command.Id);

        if (command.Active)
            coupon.Enable();
        else
            coupon.Disable();

        await db.SaveChangesAsync(ct);
        return PromotionMapper.ToCouponResponse(coupon, timeProvider.GetUtcNow().UtcDateTime);
    }
}

/// <summary>领取优惠券命令（买家端）</summary>
/// <param name="UserId">买家用户 ID</param>
/// <param name="CouponId">优惠券模板 ID</param>
public sealed record ClaimCouponCommand(Guid UserId, Guid CouponId) : ICommand<UserCouponResponse>;

/// <summary>领取优惠券命令处理器（总量 + 每人限领校验后发券）</summary>
public sealed class ClaimCouponCommandHandler(
    PromotionDbContext db,
    TimeProvider timeProvider) : ICommandHandler<ClaimCouponCommand, UserCouponResponse>
{
    /// <inheritdoc />
    public async Task<UserCouponResponse> HandleAsync(ClaimCouponCommand command, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var coupon = await db.Coupons.FirstOrDefaultAsync(c => c.Id == command.CouponId, ct)
            ?? throw new NotFoundException("优惠券", command.CouponId);

        // 每人限领校验（当前用户未使用 + 已领取数 < 限领数）
        var claimed = await db.UserCoupons.CountAsync(u => u.UserId == command.UserId && u.CouponId == command.CouponId, ct);
        if (claimed >= coupon.LimitPerUser)
            throw new DomainException("已达该券限领数量", "LIMIT_REACHED");

        // 总量校验 + 领取计数（并发极端场景下可能超发，生产可改用 SQL 原子自增 + 检查约束）
        coupon.ClaimOne(now);
        var userCoupon = new UserCoupon(command.UserId, coupon, now);
        db.UserCoupons.Add(userCoupon);
        await db.SaveChangesAsync(ct);

        return PromotionMapper.ToUserCouponResponse(userCoupon, now);
    }
}

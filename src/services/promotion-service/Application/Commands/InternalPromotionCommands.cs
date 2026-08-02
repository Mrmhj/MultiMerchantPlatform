using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using PromotionService.DTOs;
using PromotionService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PromotionService.Application.Commands;

/// <summary>核销用户优惠券命令（内部接口，order-service 支付确认时调用）</summary>
/// <param name="UserId">买家用户 ID</param>
/// <param name="UserCouponId">用户优惠券 ID</param>
/// <param name="OrderId">关联订单 ID（对账用，可空）</param>
public sealed record UseUserCouponCommand(Guid UserId, Guid UserCouponId, Guid? OrderId)
    : ICommand<UseCouponResult>;

/// <summary>核销用户优惠券命令处理器（Result 语义，失败不抛异常，返回 Error）</summary>
public sealed class UseUserCouponCommandHandler(
    PromotionDbContext db,
    TimeProvider timeProvider) : ICommandHandler<UseUserCouponCommand, UseCouponResult>
{
    /// <inheritdoc />
    public async Task<UseCouponResult> HandleAsync(UseUserCouponCommand command, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var userCoupon = await db.UserCoupons.FirstOrDefaultAsync(
            u => u.Id == command.UserCouponId && u.UserId == command.UserId, ct);

        if (userCoupon is null)
            return new UseCouponResult { Success = false, Error = "优惠券不存在", ErrorCode = "COUPON_NOT_FOUND" };

        try
        {
            userCoupon.MarkUsed(now);
        }
        catch (DomainException ex)
        {
            return new UseCouponResult { Success = false, Error = ex.Message, ErrorCode = ex.ErrorCode };
        }

        await db.SaveChangesAsync(ct);
        return new UseCouponResult
        {
            Success = true,
            DiscountAmount = userCoupon.DiscountAmount,
            UsedAt = userCoupon.UsedAt,
        };
    }
}

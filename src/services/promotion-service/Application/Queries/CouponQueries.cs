using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using BuildingBlocks.MultiTenant;
using PromotionService.DTOs;
using PromotionService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PromotionService.Application.Queries;

/// <summary>商户优惠券列表查询（分页）</summary>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
public sealed record ListCouponsQuery(int Page, int PageSize) : IQuery<PagedResult<CouponResponse>>;

/// <summary>商户优惠券列表查询处理器</summary>
public sealed class ListCouponsQueryHandler(
    PromotionDbContext db,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider) : IQueryHandler<ListCouponsQuery, PagedResult<CouponResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<CouponResponse>> HandleAsync(ListCouponsQuery query, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var total = await db.Coupons.CountAsync(c => c.MerchantId == merchantId, ct);
        var items = await db.Coupons.AsNoTracking()
            .Where(c => c.MerchantId == merchantId)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<CouponResponse>(
            items.Select(c => PromotionMapper.ToCouponResponse(c, now)).ToList(), total, page, pageSize);
    }
}

/// <summary>商户优惠券详情查询</summary>
/// <param name="Id">优惠券模板 ID</param>
public sealed record GetCouponQuery(Guid Id) : IQuery<CouponResponse>;

/// <summary>商户优惠券详情查询处理器</summary>
public sealed class GetCouponQueryHandler(
    PromotionDbContext db,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider) : IQueryHandler<GetCouponQuery, CouponResponse>
{
    /// <inheritdoc />
    public async Task<CouponResponse> HandleAsync(GetCouponQuery query, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var coupon = await db.Coupons.AsNoTracking().FirstOrDefaultAsync(
            c => c.Id == query.Id && c.MerchantId == merchantId, ct)
            ?? throw new NotFoundException("优惠券", query.Id);

        return PromotionMapper.ToCouponResponse(coupon, timeProvider.GetUtcNow().UtcDateTime);
    }
}

/// <summary>C 端可领取优惠券列表（启用 + 有效期窗口内 + 未领完）</summary>
public sealed record AvailableCouponsQuery : IQuery<List<CouponResponse>>;

/// <summary>C 端可领取优惠券列表查询处理器</summary>
public sealed class AvailableCouponsQueryHandler(
    PromotionDbContext db,
    TimeProvider timeProvider) : IQueryHandler<AvailableCouponsQuery, List<CouponResponse>>
{
    /// <inheritdoc />
    public async Task<List<CouponResponse>> HandleAsync(AvailableCouponsQuery query, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var coupons = await db.Coupons.AsNoTracking()
            .Where(c => c.Status == PromotionService.Domain.Enums.CouponStatus.Active
                        && c.ValidFrom <= now && c.ValidUntil >= now
                        && (c.TotalQuantity <= 0 || c.ClaimedCount < c.TotalQuantity))
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        return coupons.Select(c => PromotionMapper.ToCouponResponse(c, now)).ToList();
    }
}

/// <summary>我的优惠券查询（status 过滤：unused/used/expired/all）</summary>
/// <param name="UserId">买家用户 ID</param>
/// <param name="Status">过滤状态：all（默认）/unused/used/expired</param>
public sealed record MyCouponsQuery(Guid UserId, string? Status) : IQuery<List<UserCouponResponse>>;

/// <summary>我的优惠券查询处理器（过期状态按有效期推导）</summary>
public sealed class MyCouponsQueryHandler(
    PromotionDbContext db,
    TimeProvider timeProvider) : IQueryHandler<MyCouponsQuery, List<UserCouponResponse>>
{
    /// <inheritdoc />
    public async Task<List<UserCouponResponse>> HandleAsync(MyCouponsQuery query, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var userId = query.UserId;

        var list = await db.UserCoupons.AsNoTracking()
            .Where(u => u.UserId == userId)
            .OrderByDescending(u => u.ClaimedAt)
            .ToListAsync(ct);

        // 内存过滤：Expired 由有效期推导，与数据库状态组合
        var filter = query.Status?.ToLowerInvariant();
        var responses = list.Select(u => PromotionMapper.ToUserCouponResponse(u, now));
        return filter switch
        {
            "unused" => responses.Where(r => r.Status == PromotionService.Domain.Enums.UserCouponStatus.Unused).ToList(),
            "used" => responses.Where(r => r.Status == PromotionService.Domain.Enums.UserCouponStatus.Used).ToList(),
            "expired" => responses.Where(r => r.Status == PromotionService.Domain.Enums.UserCouponStatus.Expired).ToList(),
            _ => responses.ToList(),
        };
    }
}

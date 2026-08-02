using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using BuildingBlocks.MultiTenant;
using PromotionService.Domain.Enums;
using PromotionService.DTOs;
using PromotionService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PromotionService.Application.Queries;

/// <summary>商户满减活动列表查询（分页）</summary>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
/// <param name="Status">状态过滤：all（默认）/draft/active/ended</param>
public sealed record ListActivitiesQuery(int Page, int PageSize, string? Status) : IQuery<PagedResult<ActivityResponse>>;

/// <summary>商户满减活动列表查询处理器</summary>
public sealed class ListActivitiesQueryHandler(
    PromotionDbContext db,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider) : IQueryHandler<ListActivitiesQuery, PagedResult<ActivityResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<ActivityResponse>> HandleAsync(ListActivitiesQuery query, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var filter = query.Status?.ToLowerInvariant();
        var baseQuery = db.Activities.AsNoTracking().Where(a => a.MerchantId == merchantId);

        // 状态过滤（Ended 需结合时间窗口推导，先取全量在内存收尾）
        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // 时间窗口收尾（Active 且已过结束时间 → Ended）
        foreach (var activity in items)
            activity.EndIfExpired(now);
        await db.SaveChangesAsync(ct);

        var responses = items.Select(a => PromotionMapper.ToActivityResponse(a, now)).ToList();
        if (filter is not null && filter != "all")
        {
            var target = filter switch
            {
                "draft" => ActivityStatus.Draft,
                "active" => ActivityStatus.Active,
                "ended" => ActivityStatus.Ended,
                _ => (ActivityStatus?)null,
            };
            if (target is not null)
                responses = responses.Where(r => r.Status == target).ToList();
        }

        return new PagedResult<ActivityResponse>(responses, total, page, pageSize);
    }
}

/// <summary>满减活动详情查询</summary>
/// <param name="Id">活动 ID</param>
public sealed record GetActivityQuery(Guid Id) : IQuery<ActivityResponse>;

/// <summary>满减活动详情查询处理器</summary>
public sealed class GetActivityQueryHandler(
    PromotionDbContext db,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider) : IQueryHandler<GetActivityQuery, ActivityResponse>
{
    /// <inheritdoc />
    public async Task<ActivityResponse> HandleAsync(GetActivityQuery query, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var activity = await db.Activities.AsNoTracking().FirstOrDefaultAsync(
            a => a.Id == query.Id && a.MerchantId == merchantId, ct)
            ?? throw new NotFoundException("满减活动", query.Id);

        activity.EndIfExpired(now);
        await db.SaveChangesAsync(ct);
        return PromotionMapper.ToActivityResponse(activity, now);
    }
}

/// <summary>进行中满减活动查询（C 端/公开，仅 Active 且时间窗口内）</summary>
public sealed record ActiveActivitiesQuery : IQuery<List<ActivityResponse>>;

/// <summary>进行中满减活动查询处理器</summary>
public sealed class ActiveActivitiesQueryHandler(
    PromotionDbContext db,
    TimeProvider timeProvider) : IQueryHandler<ActiveActivitiesQuery, List<ActivityResponse>>
{
    /// <inheritdoc />
    public async Task<List<ActivityResponse>> HandleAsync(ActiveActivitiesQuery query, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var activities = await db.Activities.AsNoTracking()
            .Where(a => a.Status == ActivityStatus.Active
                        && a.StartTime <= now && a.EndTime >= now)
            .OrderByDescending(a => a.StartTime)
            .ToListAsync(ct);

        return activities.Select(a => PromotionMapper.ToActivityResponse(a, now)).ToList();
    }
}

using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using BuildingBlocks.MultiTenant;
using PromotionService.Domain.Enums;
using PromotionService.DTOs;
using PromotionService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PromotionService.Application.Queries;

/// <summary>商户秒杀活动列表查询（分页）</summary>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
/// <param name="Status">状态过滤：all（默认）/draft/active/ended</param>
public sealed record ListSeckillsQuery(int Page, int PageSize, string? Status)
    : IQuery<PagedResult<SeckillResponse>>;

/// <summary>商户秒杀活动列表查询处理器</summary>
public sealed class ListSeckillsQueryHandler(
    PromotionDbContext db,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider) : IQueryHandler<ListSeckillsQuery, PagedResult<SeckillResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<SeckillResponse>> HandleAsync(ListSeckillsQuery query, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var filter = query.Status?.ToLowerInvariant();

        var baseQuery = db.SeckillActivities.AsNoTracking().Where(a => a.MerchantId == merchantId);
        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        foreach (var activity in items)
            activity.EndIfExpired(now);
        await db.SaveChangesAsync(ct);

        var responses = items.Select(a => PromotionMapper.ToSeckillResponse(a, now)).ToList();
        if (filter is not null && filter != "all")
        {
            var target = filter switch
            {
                "draft" => SeckillStatus.Draft,
                "active" => SeckillStatus.Active,
                "ended" => SeckillStatus.Ended,
                _ => (SeckillStatus?)null,
            };
            if (target is not null)
                responses = responses.Where(r => r.Status == target).ToList();
        }

        return new PagedResult<SeckillResponse>(responses, total, page, pageSize);
    }
}

/// <summary>秒杀活动详情查询（商户端）</summary>
/// <param name="Id">活动 ID</param>
public sealed record GetSeckillQuery(Guid Id) : IQuery<SeckillResponse>;

/// <summary>秒杀活动详情查询处理器</summary>
public sealed class GetSeckillQueryHandler(
    PromotionDbContext db,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider) : IQueryHandler<GetSeckillQuery, SeckillResponse>
{
    /// <inheritdoc />
    public async Task<SeckillResponse> HandleAsync(GetSeckillQuery query, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var activity = await db.SeckillActivities.AsNoTracking().FirstOrDefaultAsync(
            a => a.Id == query.Id && a.MerchantId == merchantId, ct)
            ?? throw new NotFoundException("秒杀活动", query.Id);

        activity.EndIfExpired(now);
        await db.SaveChangesAsync(ct);
        return PromotionMapper.ToSeckillResponse(activity, now);
    }
}

/// <summary>进行中秒杀活动查询（C 端公开，仅 Active 且时间窗口内）</summary>
public sealed record ActiveSeckillsQuery : IQuery<List<SeckillResponse>>;

/// <summary>进行中秒杀活动查询处理器</summary>
public sealed class ActiveSeckillsQueryHandler(
    PromotionDbContext db,
    TimeProvider timeProvider) : IQueryHandler<ActiveSeckillsQuery, List<SeckillResponse>>
{
    /// <inheritdoc />
    public async Task<List<SeckillResponse>> HandleAsync(ActiveSeckillsQuery query, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var activities = await db.SeckillActivities.AsNoTracking()
            .Where(a => a.Status == SeckillStatus.Active
                        && a.StartTime <= now && a.EndTime >= now)
            .OrderByDescending(a => a.StartTime)
            .ToListAsync(ct);

        return activities.Select(a => PromotionMapper.ToSeckillResponse(a, now)).ToList();
    }
}

/// <summary>我的秒杀记录查询（买家端，分页）</summary>
/// <param name="UserId">买家用户 ID</param>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
public sealed record MySeckillRecordsQuery(Guid UserId, int Page, int PageSize)
    : IQuery<PagedResult<SeckillRecordResponse>>;

/// <summary>我的秒杀记录查询处理器</summary>
public sealed class MySeckillRecordsQueryHandler(
    PromotionDbContext db) : IQueryHandler<MySeckillRecordsQuery, PagedResult<SeckillRecordResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<SeckillRecordResponse>> HandleAsync(
        MySeckillRecordsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var baseQuery = db.SeckillRecords.AsNoTracking()
            .Where(r => r.UserId == query.UserId)
            .OrderByDescending(r => r.CreatedAt);

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<SeckillRecordResponse>(
            items.Select(PromotionMapper.ToSeckillRecordResponse).ToList(), total, page, pageSize);
    }
}

/// <summary>秒杀记录详情查询（买家端）</summary>
/// <param name="UserId">买家用户 ID</param>
/// <param name="RecordId">秒杀记录 ID</param>
public sealed record GetSeckillRecordQuery(Guid UserId, Guid RecordId)
    : IQuery<SeckillRecordResponse>;

/// <summary>秒杀记录详情查询处理器</summary>
public sealed class GetSeckillRecordQueryHandler(
    PromotionDbContext db) : IQueryHandler<GetSeckillRecordQuery, SeckillRecordResponse>
{
    /// <inheritdoc />
    public async Task<SeckillRecordResponse> HandleAsync(
        GetSeckillRecordQuery query, CancellationToken ct = default)
    {
        var record = await db.SeckillRecords.AsNoTracking().FirstOrDefaultAsync(
            r => r.Id == query.RecordId && r.UserId == query.UserId, ct)
            ?? throw new NotFoundException("秒杀记录", query.RecordId);

        return PromotionMapper.ToSeckillRecordResponse(record);
    }
}

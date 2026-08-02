using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Results;
using RiskService.Domain.Enums;
using RiskService.DTOs;
using RiskService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace RiskService.Application.Queries;

/// <summary>风控规则列表查询（平台端，分页）</summary>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
/// <param name="Scene">场景过滤（可选）</param>
/// <param name="Enabled">启用状态过滤（可选）</param>
public sealed record RiskRulesQuery(int Page, int PageSize, string? Scene, bool? Enabled)
    : IQuery<PagedResult<RiskRuleResponse>>;

/// <summary>风控规则列表查询处理器</summary>
public sealed class RiskRulesQueryHandler(
    RiskDbContext db) : IQueryHandler<RiskRulesQuery, PagedResult<RiskRuleResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<RiskRuleResponse>> HandleAsync(RiskRulesQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var baseQuery = db.RiskRules.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Scene))
            baseQuery = baseQuery.Where(r => r.Scene == query.Scene.Trim().ToUpperInvariant());
        if (query.Enabled.HasValue)
            baseQuery = baseQuery.Where(r => r.Enabled == query.Enabled.Value);

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<RiskRuleResponse>(
            items.Select(RiskMapper.ToRuleResponse).ToList(), total, page, pageSize);
    }
}

/// <summary>风险案例列表查询（平台端，分页）</summary>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
/// <param name="Status">状态过滤（可选：open/reviewing/resolved/falsepositive）</param>
/// <param name="Scene">场景过滤（可选）</param>
/// <param name="MerchantId">商户过滤（可选）</param>
/// <param name="Disposition">处置级别过滤（可选：watch/block）</param>
public sealed record RiskCasesQuery(int Page, int PageSize, string? Status, string? Scene, Guid? MerchantId, RiskDisposition? Disposition)
    : IQuery<PagedResult<RiskCaseResponse>>;

/// <summary>风险案例列表查询处理器</summary>
public sealed class RiskCasesQueryHandler(
    RiskDbContext db) : IQueryHandler<RiskCasesQuery, PagedResult<RiskCaseResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<RiskCaseResponse>> HandleAsync(RiskCasesQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var baseQuery = db.RiskCases.AsNoTracking();
        var status = query.Status?.ToLowerInvariant();
        if (status is not null and not "" and not "all")
        {
            var parsed = status switch
            {
                "open" => RiskCaseStatus.Open,
                "reviewing" => RiskCaseStatus.Reviewing,
                "resolved" => RiskCaseStatus.Resolved,
                "falsepositive" => RiskCaseStatus.FalsePositive,
                _ => (RiskCaseStatus?)null,
            };
            if (parsed.HasValue)
                baseQuery = baseQuery.Where(c => c.Status == parsed);
        }
        if (!string.IsNullOrWhiteSpace(query.Scene))
            baseQuery = baseQuery.Where(c => c.Scene == query.Scene.Trim().ToUpperInvariant());
        if (query.MerchantId.HasValue)
            baseQuery = baseQuery.Where(c => c.MerchantId == query.MerchantId);
        if (query.Disposition.HasValue)
            baseQuery = baseQuery.Where(c => c.Disposition == query.Disposition);

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<RiskCaseResponse>(
            items.Select(RiskMapper.ToCaseResponse).ToList(), total, page, pageSize);
    }
}

/// <summary>风控事件流水查询（平台端，分页）</summary>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
/// <param name="Scene">场景过滤（可选）</param>
/// <param name="UserId">用户过滤（可选）</param>
/// <param name="MerchantId">商户过滤（可选）</param>
public sealed record RiskEventsQuery(int Page, int PageSize, string? Scene, Guid? UserId, Guid? MerchantId)
    : IQuery<PagedResult<RiskEventResponse>>;

/// <summary>风控事件流水查询处理器</summary>
public sealed class RiskEventsQueryHandler(
    RiskDbContext db) : IQueryHandler<RiskEventsQuery, PagedResult<RiskEventResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<RiskEventResponse>> HandleAsync(RiskEventsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var baseQuery = db.RiskEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Scene))
            baseQuery = baseQuery.Where(e => e.Scene == query.Scene.Trim().ToUpperInvariant());
        if (query.UserId.HasValue)
            baseQuery = baseQuery.Where(e => e.UserId == query.UserId);
        if (query.MerchantId.HasValue)
            baseQuery = baseQuery.Where(e => e.MerchantId == query.MerchantId);

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(e => e.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<RiskEventResponse>(
            items.Select(RiskMapper.ToEventResponse).ToList(), total, page, pageSize);
    }
}

/// <summary>黑名单列表查询（平台端，分页）</summary>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
/// <param name="TargetType">对象类型过滤（可选）</param>
/// <param name="Enabled">启用状态过滤（可选）</param>
public sealed record BlacklistQuery(int Page, int PageSize, BlacklistTargetType? TargetType, bool? Enabled)
    : IQuery<PagedResult<BlacklistResponse>>;

/// <summary>黑名单列表查询处理器</summary>
public sealed class BlacklistQueryHandler(
    RiskDbContext db,
    TimeProvider timeProvider) : IQueryHandler<BlacklistQuery, PagedResult<BlacklistResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<BlacklistResponse>> HandleAsync(BlacklistQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var baseQuery = db.BlacklistEntries.AsNoTracking();
        if (query.TargetType.HasValue)
            baseQuery = baseQuery.Where(b => b.TargetType == query.TargetType);
        if (query.Enabled.HasValue)
            baseQuery = baseQuery.Where(b => b.Enabled == query.Enabled.Value);

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<BlacklistResponse>(
            items.Select(b => RiskMapper.ToBlacklistResponse(b, now)).ToList(), total, page, pageSize);
    }
}

/// <summary>风控概览查询（平台端）</summary>
public sealed record RiskOverviewQuery : IQuery<RiskOverviewResponse>;

/// <summary>风控概览查询处理器（规则/黑名单/案例/今日事件统计）</summary>
public sealed class RiskOverviewQueryHandler(
    RiskDbContext db,
    TimeProvider timeProvider) : IQueryHandler<RiskOverviewQuery, RiskOverviewResponse>
{
    /// <inheritdoc />
    public async Task<RiskOverviewResponse> HandleAsync(RiskOverviewQuery query, CancellationToken ct = default)
    {
        var todayStart = timeProvider.GetUtcNow().UtcDateTime.Date;

        var enabledRuleCount = await db.RiskRules.AsNoTracking().CountAsync(r => r.Enabled, ct);
        var totalRuleCount = await db.RiskRules.AsNoTracking().CountAsync(ct);
        var blacklistCount = await db.BlacklistEntries.AsNoTracking().CountAsync(ct);
        var openCaseCount = await db.RiskCases.AsNoTracking().CountAsync(c => c.Status == RiskCaseStatus.Open, ct);
        var reviewingCaseCount = await db.RiskCases.AsNoTracking().CountAsync(c => c.Status == RiskCaseStatus.Reviewing, ct);
        var todayEventCount = await db.RiskEvents.AsNoTracking().CountAsync(e => e.OccurredAt >= todayStart, ct);
        var todayHitCount = await db.RiskCases.AsNoTracking().CountAsync(c => c.CreatedAt >= todayStart, ct);

        return new RiskOverviewResponse
        {
            EnabledRuleCount = enabledRuleCount,
            TotalRuleCount = totalRuleCount,
            BlacklistCount = blacklistCount,
            OpenCaseCount = openCaseCount,
            ReviewingCaseCount = reviewingCaseCount,
            TodayEventCount = todayEventCount,
            TodayHitCount = todayHitCount,
        };
    }
}

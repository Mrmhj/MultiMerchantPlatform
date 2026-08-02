using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Results;
using SettlementService.DTOs;
using SettlementService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace SettlementService.Application.Queries;

/// <summary>商户结算单列表查询（分页，可按状态过滤）</summary>
/// <param name="MerchantId">商户 ID（X-Merchant-Id）</param>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
/// <param name="Status">状态过滤（可选：pending/settled/paid）</param>
public sealed record MerchantSettlementsQuery(Guid MerchantId, int Page, int PageSize, string? Status)
    : IQuery<PagedResult<SettlementResponse>>;

/// <summary>商户结算单列表查询处理器</summary>
public sealed class MerchantSettlementsQueryHandler(
    SettlementDbContext db) : IQueryHandler<MerchantSettlementsQuery, PagedResult<SettlementResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<SettlementResponse>> HandleAsync(MerchantSettlementsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var baseQuery = db.Settlements.AsNoTracking().Where(s => s.MerchantId == query.MerchantId);
        var status = query.Status?.ToLowerInvariant();
        if (status is not null and not "" and not "all")
        {
            var parsed = status switch
            {
                "pending" => Domain.Enums.SettlementStatus.Pending,
                "settled" => Domain.Enums.SettlementStatus.Settled,
                "paid" => Domain.Enums.SettlementStatus.Paid,
                _ => (Domain.Enums.SettlementStatus?)null,
            };
            if (parsed.HasValue)
                baseQuery = baseQuery.Where(s => s.Status == parsed);
        }

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<SettlementResponse>(
            items.Select(s => SettlementMapper.ToResponse(s, includeItems: false)).ToList(), total, page, pageSize);
    }
}

/// <summary>商户结算单详情查询（含明细）</summary>
/// <param name="MerchantId">商户 ID（X-Merchant-Id）</param>
/// <param name="SettlementId">结算单 ID</param>
public sealed record MerchantSettlementDetailQuery(Guid MerchantId, Guid SettlementId)
    : IQuery<SettlementResponse?>;

/// <summary>商户结算单详情查询处理器</summary>
public sealed class MerchantSettlementDetailQueryHandler(
    SettlementDbContext db) : IQueryHandler<MerchantSettlementDetailQuery, SettlementResponse?>
{
    /// <inheritdoc />
    public async Task<SettlementResponse?> HandleAsync(MerchantSettlementDetailQuery query, CancellationToken ct = default)
    {
        var settlement = await db.Settlements.AsNoTracking()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == query.SettlementId && s.MerchantId == query.MerchantId, ct);

        return settlement is null ? null : SettlementMapper.ToResponse(settlement, includeItems: true);
    }
}

/// <summary>商户结算概览查询</summary>
/// <param name="MerchantId">商户 ID（X-Merchant-Id）</param>
public sealed record MerchantSettlementSummaryQuery(Guid MerchantId) : IQuery<MerchantSettlementSummaryResponse>;

/// <summary>商户结算概览查询处理器（待结算/已结算/已打款金额与单数）</summary>
public sealed class MerchantSettlementSummaryQueryHandler(
    SettlementDbContext db) : IQueryHandler<MerchantSettlementSummaryQuery, MerchantSettlementSummaryResponse>
{
    /// <inheritdoc />
    public async Task<MerchantSettlementSummaryResponse> HandleAsync(MerchantSettlementSummaryQuery query, CancellationToken ct = default)
    {
        var list = await db.Settlements.AsNoTracking()
            .Where(s => s.MerchantId == query.MerchantId)
            .Select(s => new { s.Status, s.SettlementAmount, s.TotalCommission })
            .ToListAsync(ct);

        var pending = list.Where(s => s.Status == Domain.Enums.SettlementStatus.Pending).ToList();
        var settled = list.Where(s => s.Status == Domain.Enums.SettlementStatus.Settled).ToList();
        var paid = list.Where(s => s.Status == Domain.Enums.SettlementStatus.Paid).ToList();

        return new MerchantSettlementSummaryResponse
        {
            PendingCount = pending.Count,
            SettledCount = settled.Count,
            PaidCount = paid.Count,
            PendingAmount = pending.Sum(s => s.SettlementAmount),
            SettledAmount = settled.Sum(s => s.SettlementAmount) + paid.Sum(s => s.SettlementAmount),
            TotalCommission = list.Sum(s => s.TotalCommission),
        };
    }
}

/// <summary>商户佣金规则查询（无规则时返回平台默认）</summary>
/// <param name="MerchantId">商户 ID（X-Merchant-Id）</param>
public sealed record MerchantCommissionRuleQuery(Guid MerchantId) : IQuery<CommissionRuleResponse>;

/// <summary>商户佣金规则查询处理器</summary>
public sealed class MerchantCommissionRuleQueryHandler(
    SettlementDbContext db,
    IConfiguration configuration) : IQueryHandler<MerchantCommissionRuleQuery, CommissionRuleResponse>
{
    /// <inheritdoc />
    public async Task<CommissionRuleResponse> HandleAsync(MerchantCommissionRuleQuery query, CancellationToken ct = default)
    {
        var rule = await db.CommissionRules.AsNoTracking()
            .FirstOrDefaultAsync(r => r.MerchantId == query.MerchantId, ct);

        if (rule is not null)
            return SettlementMapper.ToCommissionRuleResponse(rule, isDefault: false);

        // 未配置：返回平台默认比例
        var defaultRate = configuration.GetValue<decimal>("DefaultCommissionRate", 5m);
        return new CommissionRuleResponse
        {
            MerchantId = query.MerchantId,
            Rate = defaultRate,
            IsDefault = true,
        };
    }
}

/// <summary>平台结算单列表查询（分页，可按状态/商户过滤）</summary>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
/// <param name="Status">状态过滤（可选）</param>
/// <param name="MerchantId">商户 ID 过滤（可选）</param>
public sealed record AdminSettlementsQuery(int Page, int PageSize, string? Status, Guid? MerchantId)
    : IQuery<PagedResult<SettlementResponse>>;

/// <summary>平台结算单列表查询处理器</summary>
public sealed class AdminSettlementsQueryHandler(
    SettlementDbContext db) : IQueryHandler<AdminSettlementsQuery, PagedResult<SettlementResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<SettlementResponse>> HandleAsync(AdminSettlementsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var baseQuery = db.Settlements.AsNoTracking();
        var status = query.Status?.ToLowerInvariant();
        if (status is not null and not "" and not "all")
        {
            var parsed = status switch
            {
                "pending" => Domain.Enums.SettlementStatus.Pending,
                "settled" => Domain.Enums.SettlementStatus.Settled,
                "paid" => Domain.Enums.SettlementStatus.Paid,
                _ => (Domain.Enums.SettlementStatus?)null,
            };
            if (parsed.HasValue)
                baseQuery = baseQuery.Where(s => s.Status == parsed);
        }
        if (query.MerchantId.HasValue)
            baseQuery = baseQuery.Where(s => s.MerchantId == query.MerchantId);

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<SettlementResponse>(
            items.Select(s => SettlementMapper.ToResponse(s, includeItems: false)).ToList(), total, page, pageSize);
    }
}

/// <summary>佣金规则列表查询（平台端）</summary>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
public sealed record AdminCommissionRulesQuery(int Page, int PageSize) : IQuery<PagedResult<CommissionRuleResponse>>;

/// <summary>佣金规则列表查询处理器（平台端）</summary>
public sealed class AdminCommissionRulesQueryHandler(
    SettlementDbContext db) : IQueryHandler<AdminCommissionRulesQuery, PagedResult<CommissionRuleResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<CommissionRuleResponse>> HandleAsync(AdminCommissionRulesQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var total = await db.CommissionRules.AsNoTracking().CountAsync(ct);
        var items = await db.CommissionRules.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<CommissionRuleResponse>(
            items.Select(r => SettlementMapper.ToCommissionRuleResponse(r, isDefault: false)).ToList(), total, page, pageSize);
    }
}

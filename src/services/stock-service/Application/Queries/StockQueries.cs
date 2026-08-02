using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using BuildingBlocks.MultiTenant;
using StockService.DTOs;
using StockService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace StockService.Application.Queries;

/// <summary>库存列表查询（商户，分页）</summary>
public sealed record ListStocksQuery(int Page, int PageSize) : IQuery<PagedResult<StockResponse>>;

/// <summary>库存列表查询处理器</summary>
public sealed class ListStocksQueryHandler(
    StockDbContext db,
    ITenantProvider tenantProvider) : IQueryHandler<ListStocksQuery, PagedResult<StockResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<StockResponse>> HandleAsync(ListStocksQuery query, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = db.StockItems.AsNoTracking().Where(s => s.MerchantId == merchantId);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<StockResponse>(items.Select(StockMapper.ToResponse).ToList(), total, page, pageSize);
    }
}

/// <summary>库存详情查询（商户，按 SKU）</summary>
public sealed record GetStockQuery(Guid SkuId) : IQuery<StockResponse>;

/// <summary>库存详情查询处理器</summary>
public sealed class GetStockQueryHandler(
    StockDbContext db,
    ITenantProvider tenantProvider) : IQueryHandler<GetStockQuery, StockResponse>
{
    /// <inheritdoc />
    public async Task<StockResponse> HandleAsync(GetStockQuery query, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var item = await db.StockItems.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SkuId == query.SkuId && s.MerchantId == merchantId, ct)
            ?? throw new NotFoundException("库存", query.SkuId);

        return StockMapper.ToResponse(item);
    }
}

/// <summary>库存流水查询（商户，按 SKU）</summary>
public sealed record ListTransactionsQuery(Guid SkuId, int Page, int PageSize) : IQuery<PagedResult<StockTransactionResponse>>;

/// <summary>库存流水查询处理器</summary>
public sealed class ListTransactionsQueryHandler(
    StockDbContext db,
    ITenantProvider tenantProvider) : IQueryHandler<ListTransactionsQuery, PagedResult<StockTransactionResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<StockTransactionResponse>> HandleAsync(ListTransactionsQuery query, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = db.StockTransactions.AsNoTracking()
            .Where(t => t.SkuId == query.SkuId && t.MerchantId == merchantId);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<StockTransactionResponse>(items.Select(StockMapper.ToTransactionResponse).ToList(), total, page, pageSize);
    }
}

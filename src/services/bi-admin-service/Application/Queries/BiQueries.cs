using BiAdminService.Application.Services;
using BiAdminService.Domain.Entities;
using BiAdminService.DTOs;
using BiAdminService.Infrastructure.Persistence;
using BuildingBlocks.Core.CQRS;
using Microsoft.EntityFrameworkCore;

namespace BiAdminService.Application.Queries;

/// <summary>总览查询</summary>
public sealed record BiOverviewQuery : IQuery<BiOverviewResponse>;

/// <summary>总览查询处理器</summary>
public sealed class BiOverviewQueryHandler(BiDbContext db) : IQueryHandler<BiOverviewQuery, BiOverviewResponse>
{
    /// <inheritdoc />
    public async Task<BiOverviewResponse> HandleAsync(BiOverviewQuery query, CancellationToken ct = default)
    {
        var overview = await db.Overviews.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? new BiOverview();
        return new BiOverviewResponse(
            overview.TotalGmv, overview.TotalOrders, overview.PaidOrders, overview.CompletedOrders,
            overview.MerchantCount, overview.ProductCount, overview.UserCount, overview.SyncedAt);
    }
}

/// <summary>销售趋势查询（按天）</summary>
/// <param name="Days">最近天数（默认 30，上限 90）</param>
public sealed record BiSalesTrendQuery(int Days = 30) : IQuery<List<BiSalesTrendPoint>>;

/// <summary>销售趋势查询处理器</summary>
public sealed class BiSalesTrendQueryHandler(BiDbContext db) : IQueryHandler<BiSalesTrendQuery, List<BiSalesTrendPoint>>
{
    /// <inheritdoc />
    public async Task<List<BiSalesTrendPoint>> HandleAsync(BiSalesTrendQuery query, CancellationToken ct = default)
    {
        var days = Math.Clamp(query.Days, 1, 90);
        var from = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var rows = await db.DailySales.AsNoTracking()
            .Where(d => d.Date >= from)
            .OrderBy(d => d.Date)
            .ToListAsync(ct);

        return rows.Select(d => new BiSalesTrendPoint(d.Date.ToString("yyyy-MM-dd"), d.Gmv, d.OrderCount)).ToList();
    }
}

/// <summary>商户销售排行查询</summary>
/// <param name="Top">返回条数（默认 10，上限 50）</param>
public sealed record BiMerchantRankQuery(int Top = 10) : IQuery<List<BiMerchantRankResponse>>;

/// <summary>商户销售排行查询处理器</summary>
public sealed class BiMerchantRankQueryHandler(BiDbContext db) : IQueryHandler<BiMerchantRankQuery, List<BiMerchantRankResponse>>
{
    /// <inheritdoc />
    public async Task<List<BiMerchantRankResponse>> HandleAsync(BiMerchantRankQuery query, CancellationToken ct = default)
    {
        var top = Math.Clamp(query.Top, 1, 50);
        return await db.MerchantSales.AsNoTracking()
            .OrderByDescending(m => m.Gmv)
            .Take(top)
            .Select(m => new BiMerchantRankResponse(m.MerchantId, m.MerchantName, m.Gmv, m.OrderCount))
            .ToListAsync(ct);
    }
}

/// <summary>商品销售排行查询</summary>
/// <param name="Top">返回条数（默认 10，上限 50）</param>
public sealed record BiProductRankQuery(int Top = 10) : IQuery<List<BiProductRankResponse>>;

/// <summary>商品销售排行查询处理器</summary>
public sealed class BiProductRankQueryHandler(BiDbContext db) : IQueryHandler<BiProductRankQuery, List<BiProductRankResponse>>
{
    /// <inheritdoc />
    public async Task<List<BiProductRankResponse>> HandleAsync(BiProductRankQuery query, CancellationToken ct = default)
    {
        var top = Math.Clamp(query.Top, 1, 50);
        return await db.ProductSales.AsNoTracking()
            .OrderByDescending(p => p.Amount)
            .Take(top)
            .Select(p => new BiProductRankResponse(p.ProductId, p.ProductName, p.Quantity, p.Amount))
            .ToListAsync(ct);
    }
}

/// <summary>订单状态分布查询</summary>
public sealed record BiOrderStatusQuery : IQuery<List<BiOrderStatusResponse>>;

/// <summary>订单状态分布查询处理器</summary>
public sealed class BiOrderStatusQueryHandler(BiDbContext db) : IQueryHandler<BiOrderStatusQuery, List<BiOrderStatusResponse>>
{
    /// <inheritdoc />
    public async Task<List<BiOrderStatusResponse>> HandleAsync(BiOrderStatusQuery query, CancellationToken ct = default)
    {
        return await db.OrderStatusDist.AsNoTracking()
            .OrderBy(s => s.Status)
            .Select(s => new BiOrderStatusResponse(s.Status, s.Count))
            .ToListAsync(ct);
    }
}

using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Results;
using SearchService.DTOs;
using SearchService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace SearchService.Application.Queries;

/// <summary>商品搜索查询（C 端公开，仅在售）</summary>
/// <param name="Keyword">关键词（匹配名称/描述）</param>
/// <param name="CategoryId">分类过滤（可选）</param>
/// <param name="MinPrice">最低价（可选）</param>
/// <param name="MaxPrice">最高价（可选）</param>
/// <param name="Page">页码（从 1 开始）</param>
/// <param name="PageSize">每页条数（1-100）</param>
public sealed record SearchProductsQuery(
    string? Keyword, Guid? CategoryId, decimal? MinPrice, decimal? MaxPrice, int Page, int PageSize)
    : IQuery<PagedResult<SearchResultItem>>;

/// <summary>商品搜索查询处理器（LIKE 检索，生产可升级 SQL Full-Text）</summary>
public sealed class SearchProductsQueryHandler(SearchDbContext db) : IQueryHandler<SearchProductsQuery, PagedResult<SearchResultItem>>
{
    /// <inheritdoc />
    public async Task<PagedResult<SearchResultItem>> HandleAsync(SearchProductsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // 仅在售（Status=2 OnSale）
        var q = db.Products.AsNoTracking().Where(p => p.Status == 2);

        // 关键词：名称或描述 LIKE（%kw%）
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = $"%{query.Keyword.Trim()}%";
            q = q.Where(p => EF.Functions.Like(p.Name, kw) || EF.Functions.Like(p.Description!, kw));
        }

        if (query.CategoryId.HasValue)
            q = q.Where(p => p.CategoryId == query.CategoryId.Value);

        if (query.MinPrice.HasValue)
            q = q.Where(p => p.PriceMax >= query.MinPrice.Value);

        if (query.MaxPrice.HasValue)
            q = q.Where(p => p.PriceMin <= query.MaxPrice.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<SearchResultItem>(items.Select(SearchMapper.ToResponse).ToList(), total, page, pageSize);
    }
}

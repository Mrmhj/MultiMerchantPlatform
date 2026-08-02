using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using ProductService.Domain.Enums;
using ProductService.DTOs;
using ProductService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ProductService.Application.Queries;

/// <summary>公开商品列表查询（C 端，无鉴权，仅在售商品）</summary>
public sealed record ListPublicProductsQuery(int Page, int PageSize) : IQuery<PagedResult<ProductResponse>>;

/// <summary>公开商品列表查询处理器</summary>
public sealed class ListPublicProductsQueryHandler(ProductDbContext db) : IQueryHandler<ListPublicProductsQuery, PagedResult<ProductResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<ProductResponse>> HandleAsync(ListPublicProductsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // C 端浏览：全平台在售商品（无商户过滤，HasQueryFilter 在无商户上下文时自动放行）
        var q = db.Products.AsNoTracking()
            .Include(p => p.Skus)
            .Where(p => p.Status == ProductStatus.OnSale);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ProductResponse>(items.Select(ProductMapper.ToResponse).ToList(), total, page, pageSize);
    }
}

/// <summary>公开商品详情查询（C 端，无鉴权，仅在售商品）</summary>
public sealed record GetPublicProductQuery(Guid Id) : IQuery<ProductResponse>;

/// <summary>公开商品详情查询处理器</summary>
public sealed class GetPublicProductQueryHandler(ProductDbContext db) : IQueryHandler<GetPublicProductQuery, ProductResponse>
{
    /// <inheritdoc />
    public async Task<ProductResponse> HandleAsync(GetPublicProductQuery query, CancellationToken ct = default)
    {
        var product = await db.Products.AsNoTracking()
            .Include(p => p.Skus)
            .FirstOrDefaultAsync(p => p.Id == query.Id && p.Status == ProductStatus.OnSale, ct)
            ?? throw new NotFoundException("商品", query.Id);

        return ProductMapper.ToResponse(product);
    }
}

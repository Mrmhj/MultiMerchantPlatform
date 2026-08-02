using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using BuildingBlocks.MultiTenant;
using ProductService.Domain.Entities;
using ProductService.Domain.Enums;
using ProductService.DTOs;
using ProductService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ProductService.Application.Queries;

/// <summary>分类列表查询（当前商户，树形平铺）</summary>
public sealed record ListCategoriesQuery : IQuery<IReadOnlyList<CategoryResponse>>;

/// <summary>分类列表查询处理器</summary>
public sealed class ListCategoriesQueryHandler(
    ProductDbContext db,
    ITenantProvider tenantProvider) : IQueryHandler<ListCategoriesQuery, IReadOnlyList<CategoryResponse>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryResponse>> HandleAsync(ListCategoriesQuery query, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var categories = await db.Categories.AsNoTracking()
            .Where(c => c.MerchantId == merchantId)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.CreatedAt)
            .ToListAsync(ct);

        return categories.Select(CategoryMapper.ToResponse).ToList();
    }
}

/// <summary>商品列表查询（分页 + 状态过滤，多租户隔离）</summary>
public sealed record ListProductsQuery(ProductStatus? Status, int Page, int PageSize) : IQuery<PagedResult<ProductResponse>>;

/// <summary>商品列表查询处理器</summary>
public sealed class ListProductsQueryHandler(
    ProductDbContext db,
    ITenantProvider tenantProvider) : IQueryHandler<ListProductsQuery, PagedResult<ProductResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<ProductResponse>> HandleAsync(ListProductsQuery query, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // HasQueryFilter + 显式商户过滤双保险
        var q = db.Products.AsNoTracking()
            .Include(p => p.Skus)
            .Where(p => p.MerchantId == merchantId)
            .AsQueryable();
        if (query.Status.HasValue)
            q = q.Where(p => p.Status == query.Status.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ProductResponse>(items.Select(ProductMapper.ToResponse).ToList(), total, page, pageSize);
    }
}

/// <summary>商品详情查询</summary>
public sealed record GetProductQuery(Guid Id) : IQuery<ProductResponse>;

/// <summary>商品详情查询处理器</summary>
public sealed class GetProductQueryHandler(
    ProductDbContext db,
    ITenantProvider tenantProvider) : IQueryHandler<GetProductQuery, ProductResponse>
{
    /// <inheritdoc />
    public async Task<ProductResponse> HandleAsync(GetProductQuery query, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var product = await db.Products.AsNoTracking()
            .Include(p => p.Skus)
            .FirstOrDefaultAsync(p => p.Id == query.Id && p.MerchantId == merchantId, ct)
            ?? throw new NotFoundException("商品", query.Id);

        return ProductMapper.ToResponse(product);
    }
}

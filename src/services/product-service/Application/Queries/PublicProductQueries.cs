using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using ProductService.Domain.Enums;
using ProductService.DTOs;
using ProductService.Infrastructure;
using ProductService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ProductService.Application.Queries;

/// <summary>公开商品列表查询（C 端，无鉴权，仅在售商品）</summary>
public sealed record ListPublicProductsQuery(int Page, int PageSize) : IQuery<PagedResult<ProductResponse>>;

/// <summary>公开商品列表查询处理器（批量带出商户名）</summary>
public sealed class ListPublicProductsQueryHandler(
    ProductDbContext db,
    MerchantServiceClient merchantClient) : IQueryHandler<ListPublicProductsQuery, PagedResult<ProductResponse>>
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

        // 批量查商户名（按 distinct 商户，失败不阻塞主流程）
        var names = await LoadMerchantNamesAsync(items.Select(p => p.MerchantId).Distinct(), ct);

        return new PagedResult<ProductResponse>(
            items.Select(p => ProductMapper.ToResponse(p, names.GetValueOrDefault(p.MerchantId))).ToList(),
            total, page, pageSize);
    }

    /// <summary>批量加载商户名（逐个调内部接口，一页内商户数有限）</summary>
    private async Task<Dictionary<Guid, string>> LoadMerchantNamesAsync(IEnumerable<Guid> merchantIds, CancellationToken ct)
    {
        var result = new Dictionary<Guid, string>();
        foreach (var merchantId in merchantIds)
        {
            var name = await merchantClient.GetNameAsync(merchantId, ct);
            if (!string.IsNullOrEmpty(name))
                result[merchantId] = name;
        }
        return result;
    }
}

/// <summary>公开商品详情查询（C 端，无鉴权，仅在售商品）</summary>
public sealed record GetPublicProductQuery(Guid Id) : IQuery<ProductResponse>;

/// <summary>公开商品详情查询处理器（带出商户名）</summary>
public sealed class GetPublicProductQueryHandler(
    ProductDbContext db,
    MerchantServiceClient merchantClient) : IQueryHandler<GetPublicProductQuery, ProductResponse>
{
    /// <inheritdoc />
    public async Task<ProductResponse> HandleAsync(GetPublicProductQuery query, CancellationToken ct = default)
    {
        var product = await db.Products.AsNoTracking()
            .Include(p => p.Skus)
            .FirstOrDefaultAsync(p => p.Id == query.Id && p.Status == ProductStatus.OnSale, ct)
            ?? throw new NotFoundException("商品", query.Id);

        var merchantName = await merchantClient.GetNameAsync(product.MerchantId, ct);
        return ProductMapper.ToResponse(product, merchantName);
    }
}

using BuildingBlocks.Cache;
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

/// <summary>公开商品列表查询处理器（批量带出商户名；热数据缓存 + 版本失效）</summary>
public sealed class ListPublicProductsQueryHandler(
    ProductDbContext db,
    MerchantServiceClient merchantClient,
    ICacheService cache) : IQueryHandler<ListPublicProductsQuery, PagedResult<ProductResponse>>
{
    /// <summary>列表缓存 TTL（列表变化频繁，短 TTL + 写操作版本失效双保险）</summary>
    private static readonly TimeSpan ListCacheTtl = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public async Task<PagedResult<ProductResponse>> HandleAsync(ListPublicProductsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // 版本号驱动的列表缓存：写操作自增版本 → 全部列表缓存整体失效
        // GetAsync<long> 对值类型返回 T 本身（未命中为默认 0，与「初始版本 0」语义一致）
        var version = await cache.GetAsync<long>(ProductCacheKeys.ListVersionKey, ct);
        var cacheKey = ProductCacheKeys.List(page, pageSize, version);

        var result = await cache.GetOrAddAsync(cacheKey, async token =>
        {
            // C 端浏览：全平台在售商品（无商户过滤，HasQueryFilter 在无商户上下文时自动放行）
            var q = db.Products.AsNoTracking()
                .Include(p => p.Skus)
                .Where(p => p.Status == ProductStatus.OnSale);

            var total = await q.CountAsync(token);
            var items = await q
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(token);

            // 批量查商户名（按 distinct 商户，失败不阻塞主流程）
            var names = await LoadMerchantNamesAsync(items.Select(p => p.MerchantId).Distinct(), token);

            return new PagedResult<ProductResponse>(
                items.Select(p => ProductMapper.ToResponse(p, names.GetValueOrDefault(p.MerchantId))).ToList(),
                total, page, pageSize);
        }, ListCacheTtl, ct);

        return result ?? new PagedResult<ProductResponse>([], 0, page, pageSize);
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

/// <summary>公开商品详情查询处理器（带出商户名；热数据缓存）</summary>
public sealed class GetPublicProductQueryHandler(
    ProductDbContext db,
    MerchantServiceClient merchantClient,
    ICacheService cache) : IQueryHandler<GetPublicProductQuery, ProductResponse>
{
    /// <summary>详情缓存 TTL（商品信息低频变更，较长 TTL + 写操作主动失效）</summary>
    private static readonly TimeSpan DetailCacheTtl = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public async Task<ProductResponse> HandleAsync(GetPublicProductQuery query, CancellationToken ct = default)
    {
        var response = await cache.GetOrAddAsync(ProductCacheKeys.Detail(query.Id), async token =>
        {
            var product = await db.Products.AsNoTracking()
                .Include(p => p.Skus)
                .FirstOrDefaultAsync(p => p.Id == query.Id && p.Status == ProductStatus.OnSale, token)
                ?? throw new NotFoundException("商品", query.Id);

            var merchantName = await merchantClient.GetNameAsync(product.MerchantId, token);
            return ProductMapper.ToResponse(product, merchantName);
        }, DetailCacheTtl, ct);

        return response!;
    }
}

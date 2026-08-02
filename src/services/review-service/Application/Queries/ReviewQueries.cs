using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using BuildingBlocks.MultiTenant;
using ReviewService.DTOs;
using ReviewService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ReviewService.Application.Queries;

/// <summary>我的评价列表查询（买家端，分页）</summary>
/// <param name="UserId">买家用户 ID</param>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
public sealed record MyReviewsQuery(Guid UserId, int Page, int PageSize) : IQuery<PagedResult<ReviewResponse>>;

/// <summary>我的评价列表查询处理器</summary>
public sealed class MyReviewsQueryHandler(
    ReviewDbContext db) : IQueryHandler<MyReviewsQuery, PagedResult<ReviewResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<ReviewResponse>> HandleAsync(MyReviewsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var total = await db.Reviews.CountAsync(x => x.UserId == query.UserId, ct);
        var items = await db.Reviews.AsNoTracking()
            .Where(x => x.UserId == query.UserId)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ReviewResponse>(items.Select(ReviewMapper.ToResponse).ToList(), total, page, pageSize);
    }
}

/// <summary>商户评价列表查询（分页，可按商品/评分/状态过滤）</summary>
/// <param name="MerchantId">商户 ID（X-Merchant-Id）</param>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
/// <param name="ProductId">商品 ID 过滤（可选）</param>
/// <param name="Rating">评分过滤 1-5（可选）</param>
/// <param name="Status">状态过滤：all（默认）/visible/hidden</param>
public sealed record MerchantReviewsQuery(Guid MerchantId, int Page, int PageSize,
    Guid? ProductId, int? Rating, string? Status) : IQuery<PagedResult<ReviewResponse>>;

/// <summary>商户评价列表查询处理器</summary>
public sealed class MerchantReviewsQueryHandler(
    ReviewDbContext db) : IQueryHandler<MerchantReviewsQuery, PagedResult<ReviewResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<ReviewResponse>> HandleAsync(MerchantReviewsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var baseQuery = db.Reviews.AsNoTracking().Where(x => x.MerchantId == query.MerchantId);
        if (query.ProductId.HasValue)
            baseQuery = baseQuery.Where(x => x.ProductId == query.ProductId);
        if (query.Rating is >= 1 and <= 5)
            baseQuery = baseQuery.Where(x => x.Rating == query.Rating);

        var statusFilter = query.Status?.ToLowerInvariant();
        if (statusFilter == "visible")
            baseQuery = baseQuery.Where(x => x.Status == ReviewService.Domain.Enums.ReviewStatus.Visible);
        else if (statusFilter == "hidden")
            baseQuery = baseQuery.Where(x => x.Status == ReviewService.Domain.Enums.ReviewStatus.Hidden);

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ReviewResponse>(items.Select(ReviewMapper.ToResponse).ToList(), total, page, pageSize);
    }
}

/// <summary>商品评价列表查询（C 端公开，仅可见 + 评分统计）</summary>
/// <param name="ProductId">商品 ID</param>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
/// <param name="Rating">评分过滤 1-5（可选）</param>
public sealed record ProductReviewsQuery(Guid ProductId, int Page, int PageSize, int? Rating)
    : IQuery<ProductReviewsResponse>;

/// <summary>商品评价列表查询处理器（评分统计仅统计可见评价）</summary>
public sealed class ProductReviewsQueryHandler(
    ReviewDbContext db) : IQueryHandler<ProductReviewsQuery, ProductReviewsResponse>
{
    /// <inheritdoc />
    public async Task<ProductReviewsResponse> HandleAsync(ProductReviewsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var visible = db.Reviews.AsNoTracking().Where(x => x.ProductId == query.ProductId
            && x.Status == ReviewService.Domain.Enums.ReviewStatus.Visible);
        if (query.Rating is >= 1 and <= 5)
            visible = visible.Where(x => x.Rating == query.Rating);

        // 评分统计（全部可见评价）
        var all = await db.Reviews.AsNoTracking()
            .Where(x => x.ProductId == query.ProductId
                && x.Status == ReviewService.Domain.Enums.ReviewStatus.Visible)
            .Select(x => x.Rating)
            .ToListAsync(ct);

        var total = await visible.CountAsync(ct);
        var items = await visible
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var average = all.Count > 0 ? (decimal)Math.Round(all.Average(), 1) : 0;
        var distribution = Enumerable.Range(1, 5).ToDictionary(
            r => r, r => all.Count(x => x == r));

        return new ProductReviewsResponse
        {
            ProductId = query.ProductId,
            AverageRating = average,
            TotalCount = total,
            RatingDistribution = distribution,
            Items = items.Select(ReviewMapper.ToResponse).ToList(),
        };
    }
}

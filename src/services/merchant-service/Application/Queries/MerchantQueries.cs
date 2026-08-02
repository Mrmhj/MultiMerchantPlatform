using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Security;
using MerchantService.DTOs;
using MerchantService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MerchantService.Application.Queries;

/// <summary>我的商户查询 — 返回当前登录用户的商户（含待审核/已通过/已驳回）</summary>
public sealed record GetMyMerchantQuery : IQuery<MerchantResponse?>;

/// <summary>我的商户查询处理器</summary>
public sealed class GetMyMerchantQueryHandler(
    MerchantDbContext db,
    ICurrentUser currentUser) : IQueryHandler<GetMyMerchantQuery, MerchantResponse?>
{
    /// <inheritdoc />
    public async Task<MerchantResponse?> HandleAsync(GetMyMerchantQuery query, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty)
            throw new DomainException("未认证或令牌无效", "UNAUTHENTICATED");

        var merchant = await db.Merchants.AsNoTracking()
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(m => m.OwnerUserId == currentUser.UserId, ct);

        return merchant is null ? null : MerchantMapper.ToResponse(merchant);
    }
}

/// <summary>商户详情查询（管理员）</summary>
public sealed record GetMerchantByIdQuery(Guid MerchantId) : IQuery<MerchantResponse>;

/// <summary>商户详情查询处理器</summary>
public sealed class GetMerchantByIdQueryHandler(MerchantDbContext db) : IQueryHandler<GetMerchantByIdQuery, MerchantResponse>
{
    /// <inheritdoc />
    public async Task<MerchantResponse> HandleAsync(GetMerchantByIdQuery query, CancellationToken ct = default)
    {
        var merchant = await db.Merchants.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == query.MerchantId, ct);
        if (merchant is null)
            throw new NotFoundException("商户", query.MerchantId);

        return MerchantMapper.ToResponse(merchant);
    }
}

/// <summary>商户列表查询（管理员，分页 + 状态过滤）</summary>
public sealed record ListMerchantsQuery(
    MerchantService.Domain.Enums.MerchantStatus? Status,
    int Page,
    int PageSize) : IQuery<BuildingBlocks.Core.Results.PagedResult<MerchantResponse>>;

/// <summary>商户列表查询处理器</summary>
public sealed class ListMerchantsQueryHandler(MerchantDbContext db) : IQueryHandler<ListMerchantsQuery, BuildingBlocks.Core.Results.PagedResult<MerchantResponse>>
{
    /// <inheritdoc />
    public async Task<BuildingBlocks.Core.Results.PagedResult<MerchantResponse>> HandleAsync(ListMerchantsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = db.Merchants.AsNoTracking().AsQueryable();
        if (query.Status.HasValue)
            q = q.Where(m => m.Status == query.Status.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new BuildingBlocks.Core.Results.PagedResult<MerchantResponse>(
            items.Select(MerchantMapper.ToResponse).ToList(), total, page, pageSize);
    }
}

using BuildingBlocks.Core.CQRS;
using CartService.DTOs;
using CartService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CartService.Application.Queries;

/// <summary>我的购物车查询</summary>
/// <param name="UserId">买家用户 ID</param>
public sealed record GetMyCartQuery(Guid UserId) : IQuery<CartResponse>;

/// <summary>我的购物车查询处理器</summary>
public sealed class GetMyCartQueryHandler(CartDbContext db) : IQueryHandler<GetMyCartQuery, CartResponse>
{
    /// <inheritdoc />
    public async Task<CartResponse> HandleAsync(GetMyCartQuery query, CancellationToken ct = default)
    {
        var items = await db.CartItems.AsNoTracking()
            .Where(c => c.UserId == query.UserId)
            .OrderBy(c => c.MerchantName)
            .ThenBy(c => c.CreatedAt)
            .ToListAsync(ct);

        var responses = items.Select(CartMapper.ToResponse).ToList();
        return new CartResponse
        {
            Items = responses,
            TotalCount = responses.Count,
            SelectedCount = responses.Count(c => c.IsSelected),
            SelectedTotal = responses.Where(c => c.IsSelected).Sum(c => c.Subtotal),
        };
    }
}

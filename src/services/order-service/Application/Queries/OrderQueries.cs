using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using BuildingBlocks.MultiTenant;
using BuildingBlocks.Security;
using OrderService.Domain.Enums;
using OrderService.DTOs;
using OrderService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace OrderService.Application.Queries;

/// <summary>我的订单列表查询（买家）</summary>
public sealed record ListMyOrdersQuery(int Page, int PageSize) : IQuery<PagedResult<OrderResponse>>;

/// <summary>我的订单列表查询处理器</summary>
public sealed class ListMyOrdersQueryHandler(
    OrderDbContext db,
    ICurrentUser currentUser) : IQueryHandler<ListMyOrdersQuery, PagedResult<OrderResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<OrderResponse>> HandleAsync(ListMyOrdersQuery query, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty)
            throw new DomainException("请先登录", "UNAUTHENTICATED");

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = db.Orders.AsNoTracking()
            .Include(o => o.SubOrders).ThenInclude(s => s.Items)
            .Where(o => o.BuyerUserId == currentUser.UserId);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<OrderResponse>(items.Select(OrderMapper.ToResponse).ToList(), total, page, pageSize);
    }
}

/// <summary>订单详情查询（买家）</summary>
public sealed record GetOrderQuery(Guid OrderId) : IQuery<OrderResponse>;

/// <summary>订单详情查询处理器</summary>
public sealed class GetOrderQueryHandler(
    OrderDbContext db,
    ICurrentUser currentUser) : IQueryHandler<GetOrderQuery, OrderResponse>
{
    /// <inheritdoc />
    public async Task<OrderResponse> HandleAsync(GetOrderQuery query, CancellationToken ct = default)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.SubOrders).ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(o => o.Id == query.OrderId, ct)
            ?? throw new NotFoundException("订单", query.OrderId);

        if (order.BuyerUserId != currentUser.UserId)
            throw new DomainException("无权查看该订单", "FORBIDDEN");

        return OrderMapper.ToResponse(order);
    }
}

/// <summary>商户订单列表查询（X-Merchant-Id，子单维度）</summary>
public sealed record ListMerchantSubOrdersQuery(SubOrderStatus? Status, int Page, int PageSize) : IQuery<PagedResult<SubOrderResponse>>;

/// <summary>商户订单列表查询处理器</summary>
public sealed class ListMerchantSubOrdersQueryHandler(
    OrderDbContext db,
    ITenantProvider tenantProvider) : IQueryHandler<ListMerchantSubOrdersQuery, PagedResult<SubOrderResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<SubOrderResponse>> HandleAsync(ListMerchantSubOrdersQuery query, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = db.SubOrders.AsNoTracking()
            .Include(s => s.Items)
            .Where(s => s.MerchantId == merchantId);
        if (query.Status.HasValue)
            q = q.Where(s => s.Status == query.Status.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<SubOrderResponse>(items.Select(OrderMapper.ToSubOrderResponse).ToList(), total, page, pageSize);
    }
}

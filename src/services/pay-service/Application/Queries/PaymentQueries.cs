using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using BuildingBlocks.Security;
using PayService.DTOs;
using PayService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PayService.Application.Queries;

/// <summary>我的支付单列表查询（买家）</summary>
public sealed record ListMyPaymentsQuery(int Page, int PageSize) : IQuery<PagedResult<PaymentResponse>>;

/// <summary>我的支付单列表查询处理器</summary>
public sealed class ListMyPaymentsQueryHandler(
    PayDbContext db,
    ICurrentUser currentUser) : IQueryHandler<ListMyPaymentsQuery, PagedResult<PaymentResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<PaymentResponse>> HandleAsync(ListMyPaymentsQuery query, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty)
            throw new DomainException("请先登录", "UNAUTHENTICATED");

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = db.Payments.AsNoTracking().Where(p => p.BuyerUserId == currentUser.UserId);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<PaymentResponse>(items.Select(PaymentMapper.ToResponse).ToList(), total, page, pageSize);
    }
}

/// <summary>支付单详情查询（买家）</summary>
public sealed record GetPaymentQuery(Guid PaymentId) : IQuery<PaymentResponse>;

/// <summary>支付单详情查询处理器</summary>
public sealed class GetPaymentQueryHandler(
    PayDbContext db,
    ICurrentUser currentUser) : IQueryHandler<GetPaymentQuery, PaymentResponse>
{
    /// <inheritdoc />
    public async Task<PaymentResponse> HandleAsync(GetPaymentQuery query, CancellationToken ct = default)
    {
        var payment = await db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == query.PaymentId, ct)
            ?? throw new NotFoundException("支付单", query.PaymentId);

        if (payment.BuyerUserId != currentUser.UserId)
            throw new DomainException("无权查看该支付单", "FORBIDDEN");

        return PaymentMapper.ToResponse(payment);
    }
}

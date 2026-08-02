using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Results;
using LogisticsService.DTOs;
using LogisticsService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LogisticsService.Application.Queries;

/// <summary>商户运单列表查询（分页，可按状态过滤）</summary>
/// <param name="MerchantId">商户 ID（X-Merchant-Id）</param>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
/// <param name="Status">状态过滤（可选：created/intransit/outfordelivery/signed/exception）</param>
public sealed record MerchantShipmentsQuery(Guid MerchantId, int Page, int PageSize, string? Status)
    : IQuery<PagedResult<ShipmentResponse>>;

/// <summary>商户运单列表查询处理器</summary>
public sealed class MerchantShipmentsQueryHandler(
    LogisticsDbContext db) : IQueryHandler<MerchantShipmentsQuery, PagedResult<ShipmentResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<ShipmentResponse>> HandleAsync(MerchantShipmentsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var baseQuery = db.Shipments.AsNoTracking().Where(s => s.MerchantId == query.MerchantId);
        var status = query.Status?.ToLowerInvariant();
        if (status is not null and not "" and not "all")
        {
            var parsed = status switch
            {
                "created" => Domain.Enums.ShipmentStatus.Created,
                "intransit" => Domain.Enums.ShipmentStatus.InTransit,
                "outfordelivery" => Domain.Enums.ShipmentStatus.OutForDelivery,
                "signed" => Domain.Enums.ShipmentStatus.Signed,
                "exception" => Domain.Enums.ShipmentStatus.Exception,
                _ => (Domain.Enums.ShipmentStatus?)null,
            };
            if (parsed.HasValue)
                baseQuery = baseQuery.Where(s => s.Status == parsed);
        }

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ShipmentResponse>(
            items.Select(s => ShipmentMapper.ToResponse(s, includeTracks: false)).ToList(), total, page, pageSize);
    }
}

/// <summary>商户运单详情查询（含轨迹）</summary>
/// <param name="MerchantId">商户 ID（X-Merchant-Id）</param>
/// <param name="ShipmentId">运单 ID</param>
public sealed record MerchantShipmentDetailQuery(Guid MerchantId, Guid ShipmentId) : IQuery<ShipmentResponse?>;

/// <summary>商户运单详情查询处理器</summary>
public sealed class MerchantShipmentDetailQueryHandler(
    LogisticsDbContext db) : IQueryHandler<MerchantShipmentDetailQuery, ShipmentResponse?>
{
    /// <inheritdoc />
    public async Task<ShipmentResponse?> HandleAsync(MerchantShipmentDetailQuery query, CancellationToken ct = default)
    {
        var shipment = await db.Shipments.AsNoTracking()
            .Include(s => s.Tracks)
            .FirstOrDefaultAsync(s => s.Id == query.ShipmentId && s.MerchantId == query.MerchantId, ct);

        return shipment is null ? null : ShipmentMapper.ToResponse(shipment, includeTracks: true);
    }
}

/// <summary>买家子订单运单查询（含轨迹）</summary>
/// <param name="BuyerUserId">买家用户 ID（JWT）</param>
/// <param name="SubOrderId">子订单 ID</param>
public sealed record BuyerShipmentQuery(Guid BuyerUserId, Guid SubOrderId) : IQuery<ShipmentResponse?>;

/// <summary>买家子订单运单查询处理器（显式过滤买家归属）</summary>
public sealed class BuyerShipmentQueryHandler(
    LogisticsDbContext db) : IQueryHandler<BuyerShipmentQuery, ShipmentResponse?>
{
    /// <inheritdoc />
    public async Task<ShipmentResponse?> HandleAsync(BuyerShipmentQuery query, CancellationToken ct = default)
    {
        var shipment = await db.Shipments.AsNoTracking()
            .Include(s => s.Tracks)
            .FirstOrDefaultAsync(s => s.SubOrderId == query.SubOrderId && s.BuyerUserId == query.BuyerUserId, ct);

        return shipment is null ? null : ShipmentMapper.ToResponse(shipment, includeTracks: true);
    }
}

/// <summary>启用物流公司列表查询（商户发货时选择）</summary>
public sealed record EnabledCompaniesQuery : IQuery<List<CompanyResponse>>;

/// <summary>启用物流公司列表查询处理器</summary>
public sealed class EnabledCompaniesQueryHandler(
    LogisticsDbContext db) : IQueryHandler<EnabledCompaniesQuery, List<CompanyResponse>>
{
    /// <inheritdoc />
    public async Task<List<CompanyResponse>> HandleAsync(EnabledCompaniesQuery query, CancellationToken ct = default)
    {
        var companies = await db.Companies.AsNoTracking()
            .Where(c => c.IsEnabled)
            .OrderBy(c => c.Code)
            .ToListAsync(ct);

        return companies.Select(ShipmentMapper.ToCompanyResponse).ToList();
    }
}

/// <summary>物流公司列表查询（平台端，含停用）</summary>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
public sealed record CompanyListQuery(int Page, int PageSize) : IQuery<PagedResult<CompanyResponse>>;

/// <summary>物流公司列表查询处理器（平台端）</summary>
public sealed class CompanyListQueryHandler(
    LogisticsDbContext db) : IQueryHandler<CompanyListQuery, PagedResult<CompanyResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<CompanyResponse>> HandleAsync(CompanyListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var total = await db.Companies.AsNoTracking().CountAsync(ct);
        var items = await db.Companies.AsNoTracking()
            .OrderBy(c => c.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<CompanyResponse>(
            items.Select(ShipmentMapper.ToCompanyResponse).ToList(), total, page, pageSize);
    }
}

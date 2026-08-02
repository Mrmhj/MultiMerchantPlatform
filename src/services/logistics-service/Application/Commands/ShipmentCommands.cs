using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using LogisticsService.Domain.Entities;
using LogisticsService.DTOs;
using LogisticsService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LogisticsService.Application.Commands;

/// <summary>创建运单内部命令（order-service 发货回调）</summary>
/// <param name="Request">运单信息</param>
public sealed record CreateShipmentCommand(CreateShipmentInternalRequest Request) : ICommand<ShipmentResponse>;

/// <summary>创建运单命令处理器（同一子订单仅一条运单）</summary>
public sealed class CreateShipmentCommandHandler(
    LogisticsDbContext db,
    TimeProvider timeProvider) : ICommandHandler<CreateShipmentCommand, ShipmentResponse>
{
    /// <inheritdoc />
    public async Task<ShipmentResponse> HandleAsync(CreateShipmentCommand command, CancellationToken ct = default)
    {
        var r = command.Request;

        // 防重复：同一子订单仅允许一条运单
        var exists = await db.Shipments.AnyAsync(s => s.SubOrderId == r.SubOrderId, ct);
        if (exists)
            throw new DomainException("该子订单已存在运单，不能重复创建", "SHIPMENT_ALREADY_EXISTS");

        // 防重复：同一运单号唯一
        var trackingExists = await db.Shipments.AnyAsync(s => s.TrackingNo == r.TrackingNo, ct);
        if (trackingExists)
            throw new DomainException("运单号已存在", "TRACKING_NO_EXISTS");

        // 按物流公司编码带出名称快照（公司停用/不存在时使用编码）
        var company = await db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == r.CarrierCode, ct);
        var carrierName = company?.Name ?? r.CarrierCode;

        var shipment = new Shipment(r.BuyerUserId, r.MerchantId, r.SubOrderId, r.OrderId,
            r.OrderNo, r.CarrierCode, carrierName, r.TrackingNo,
            timeProvider.GetUtcNow().UtcDateTime);

        db.Shipments.Add(shipment);
        await db.SaveChangesAsync(ct);

        return ShipmentMapper.ToResponse(shipment, includeTracks: true);
    }
}

/// <summary>轨迹推进内部命令（模拟物流公司回调）</summary>
/// <param name="TrackingNo">运单号</param>
/// <param name="Description">轨迹描述（可选）</param>
/// <param name="Location">地点（可选）</param>
/// <param name="MarkException">是否标记异常</param>
public sealed record AdvanceTrackCommand(string TrackingNo, string? Description, string? Location, bool MarkException)
    : ICommand<ShipmentResponse>;

/// <summary>轨迹推进命令处理器（按运单号推进状态，演示物流回调）</summary>
public sealed class AdvanceTrackCommandHandler(
    LogisticsDbContext db,
    TimeProvider timeProvider) : ICommandHandler<AdvanceTrackCommand, ShipmentResponse>
{
    /// <inheritdoc />
    public async Task<ShipmentResponse> HandleAsync(AdvanceTrackCommand command, CancellationToken ct = default)
    {
        var shipment = await db.Shipments
            .Include(s => s.Tracks)
            .FirstOrDefaultAsync(s => s.TrackingNo == command.TrackingNo, ct)
            ?? throw new NotFoundException("运单", command.TrackingNo);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var description = command.Description ?? string.Empty;

        // 推进/标记异常，产生新轨迹
        var track = command.MarkException
            ? shipment.MarkException(description, command.Location, now)
            : shipment.Advance(description, command.Location, now);

        // 显式标记 Added：客户端 Guid 主键的新实体被 EF 推断为 Unchanged（已存在）会导致误判 UPDATE，
        // 必须通过 Add 显式标记为新实体（充血模型下由 Handler 完成）
        db.Tracks.Add(track);

        await db.SaveChangesAsync(ct);
        return ShipmentMapper.ToResponse(shipment, includeTracks: true);
    }
}

/// <summary>创建物流公司命令（平台端）</summary>
/// <param name="Request">公司信息</param>
public sealed record CreateCompanyCommand(SaveCompanyRequest Request) : ICommand<CompanyResponse>;

/// <summary>创建物流公司命令处理器（编码唯一）</summary>
public sealed class CreateCompanyCommandHandler(
    LogisticsDbContext db) : ICommandHandler<CreateCompanyCommand, CompanyResponse>
{
    /// <inheritdoc />
    public async Task<CompanyResponse> HandleAsync(CreateCompanyCommand command, CancellationToken ct = default)
    {
        var r = command.Request;
        if (string.IsNullOrWhiteSpace(r.Code))
            throw new DomainException("物流公司编码不能为空", "INVALID_COMPANY_CODE");

        var exists = await db.Companies.AnyAsync(c => c.Code == r.Code.Trim().ToUpperInvariant(), ct);
        if (exists)
            throw new DomainException("物流公司编码已存在", "COMPANY_CODE_EXISTS");

        var company = new LogisticsCompany(r.Code, r.Name, r.TrackingUrlTemplate);
        db.Companies.Add(company);
        await db.SaveChangesAsync(ct);

        return ShipmentMapper.ToCompanyResponse(company);
    }
}

/// <summary>更新物流公司命令（平台端）</summary>
/// <param name="Id">公司 ID</param>
/// <param name="Request">公司信息</param>
public sealed record UpdateCompanyCommand(Guid Id, SaveCompanyRequest Request) : ICommand<CompanyResponse>;

/// <summary>更新物流公司命令处理器</summary>
public sealed class UpdateCompanyCommandHandler(
    LogisticsDbContext db) : ICommandHandler<UpdateCompanyCommand, CompanyResponse>
{
    /// <inheritdoc />
    public async Task<CompanyResponse> HandleAsync(UpdateCompanyCommand command, CancellationToken ct = default)
    {
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == command.Id, ct)
            ?? throw new NotFoundException("物流公司", command.Id);

        company.ChangeName(command.Request.Name);
        company.ChangeTrackingUrl(command.Request.TrackingUrlTemplate);
        await db.SaveChangesAsync(ct);

        return ShipmentMapper.ToCompanyResponse(company);
    }
}

/// <summary>物流公司启用/停用命令（平台端）</summary>
/// <param name="Id">公司 ID</param>
/// <param name="Enabled">启用 true / 停用 false</param>
public sealed record ToggleCompanyCommand(Guid Id, bool Enabled) : ICommand<CompanyResponse>;

/// <summary>物流公司启用/停用命令处理器</summary>
public sealed class ToggleCompanyCommandHandler(
    LogisticsDbContext db) : ICommandHandler<ToggleCompanyCommand, CompanyResponse>
{
    /// <inheritdoc />
    public async Task<CompanyResponse> HandleAsync(ToggleCompanyCommand command, CancellationToken ct = default)
    {
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == command.Id, ct)
            ?? throw new NotFoundException("物流公司", command.Id);

        if (command.Enabled)
            company.Enable();
        else
            company.Disable();

        await db.SaveChangesAsync(ct);
        return ShipmentMapper.ToCompanyResponse(company);
    }
}

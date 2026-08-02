using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.MultiTenant;
using StockService.Domain.Entities;
using StockService.Domain.Enums;
using StockService.DTOs;
using StockService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace StockService.Application.Commands;

/// <summary>创建库存命令（商户）</summary>
public sealed record CreateStockCommand(Guid SkuId, int Total) : ICommand<StockResponse>;

/// <summary>创建库存命令处理器</summary>
public sealed class CreateStockCommandHandler(
    StockDbContext db,
    ITenantProvider tenantProvider) : ICommandHandler<CreateStockCommand, StockResponse>
{
    /// <inheritdoc />
    public async Task<StockResponse> HandleAsync(CreateStockCommand command, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var exists = await db.StockItems.AnyAsync(s => s.SkuId == command.SkuId && s.MerchantId == merchantId, ct);
        if (exists)
            throw new DomainException("该 SKU 已创建库存", "SKU_EXISTS");

        var item = new StockItem(merchantId, command.SkuId, command.Total);
        db.StockItems.Add(item);
        db.StockTransactions.Add(item.RecordTransaction(StockTransactionType.Create, command.Total));
        await db.SaveChangesAsync(ct);

        return StockMapper.ToResponse(item);
    }
}

/// <summary>补货命令（商户）</summary>
public sealed record IncreaseStockCommand(Guid SkuId, int Quantity) : ICommand<StockResponse>;

/// <summary>补货命令处理器</summary>
public sealed class IncreaseStockCommandHandler(
    StockDbContext db,
    ITenantProvider tenantProvider) : ICommandHandler<IncreaseStockCommand, StockResponse>
{
    /// <inheritdoc />
    public async Task<StockResponse> HandleAsync(IncreaseStockCommand command, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var item = await db.StockItems.FirstOrDefaultAsync(
            s => s.SkuId == command.SkuId && s.MerchantId == merchantId, ct)
            ?? throw new NotFoundException("库存", command.SkuId);

        item.Increase(command.Quantity);
        db.StockTransactions.Add(item.RecordTransaction(StockTransactionType.Increase, command.Quantity));
        await db.SaveChangesAsync(ct);

        return StockMapper.ToResponse(item);
    }
}

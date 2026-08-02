using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.MultiTenant;
using StockService.Domain.Entities;
using StockService.Domain.Enums;
using StockService.DTOs;
using StockService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace StockService.Application.Commands;

/// <summary>内部库存操作结果</summary>
public sealed record StockOperationResult(bool Success, string? Error, StockResponse? Stock);

/// <summary>内部预占命令（订单下单回调，X-Internal-Key）</summary>
public sealed record InternalReserveCommand(Guid SkuId, int Quantity, string? ReferenceId) : ICommand<StockOperationResult>;

/// <summary>内部预占命令处理器</summary>
public sealed class InternalReserveCommandHandler(StockDbContext db) : ICommandHandler<InternalReserveCommand, StockOperationResult>
{
    /// <inheritdoc />
    public async Task<StockOperationResult> HandleAsync(InternalReserveCommand command, CancellationToken ct = default)
    {
        var item = await db.StockItems.FirstOrDefaultAsync(s => s.SkuId == command.SkuId, ct);
        if (item is null)
            return new StockOperationResult(false, "SKU 库存不存在", null);

        try
        {
            item.Reserve(command.Quantity);
            db.StockTransactions.Add(item.RecordTransaction(StockTransactionType.Reserve, command.Quantity, command.ReferenceId));
            await db.SaveChangesAsync(ct);
            return new StockOperationResult(true, null, StockMapper.ToResponse(item));
        }
        catch (InvalidOperationException ex)
        {
            return new StockOperationResult(false, ex.Message, null);
        }
    }
}

/// <summary>内部确认扣减命令（支付成功回调）</summary>
public sealed record InternalConfirmCommand(Guid SkuId, int Quantity, string? ReferenceId) : ICommand<StockOperationResult>;

/// <summary>内部确认扣减命令处理器</summary>
public sealed class InternalConfirmCommandHandler(StockDbContext db) : ICommandHandler<InternalConfirmCommand, StockOperationResult>
{
    /// <inheritdoc />
    public async Task<StockOperationResult> HandleAsync(InternalConfirmCommand command, CancellationToken ct = default)
    {
        var item = await db.StockItems.FirstOrDefaultAsync(s => s.SkuId == command.SkuId, ct);
        if (item is null)
            return new StockOperationResult(false, "SKU 库存不存在", null);

        try
        {
            item.ConfirmReservation(command.Quantity);
            db.StockTransactions.Add(item.RecordTransaction(StockTransactionType.Confirm, command.Quantity, command.ReferenceId));
            await db.SaveChangesAsync(ct);
            return new StockOperationResult(true, null, StockMapper.ToResponse(item));
        }
        catch (InvalidOperationException ex)
        {
            return new StockOperationResult(false, ex.Message, null);
        }
    }
}

/// <summary>内部释放预占命令（订单取消回调）</summary>
public sealed record InternalReleaseCommand(Guid SkuId, int Quantity, string? ReferenceId) : ICommand<StockOperationResult>;

/// <summary>内部释放预占命令处理器</summary>
public sealed class InternalReleaseCommandHandler(StockDbContext db) : ICommandHandler<InternalReleaseCommand, StockOperationResult>
{
    /// <inheritdoc />
    public async Task<StockOperationResult> HandleAsync(InternalReleaseCommand command, CancellationToken ct = default)
    {
        var item = await db.StockItems.FirstOrDefaultAsync(s => s.SkuId == command.SkuId, ct);
        if (item is null)
            return new StockOperationResult(false, "SKU 库存不存在", null);

        try
        {
            item.ReleaseReservation(command.Quantity);
            db.StockTransactions.Add(item.RecordTransaction(StockTransactionType.Release, command.Quantity, command.ReferenceId));
            await db.SaveChangesAsync(ct);
            return new StockOperationResult(true, null, StockMapper.ToResponse(item));
        }
        catch (InvalidOperationException ex)
        {
            return new StockOperationResult(false, ex.Message, null);
        }
    }
}

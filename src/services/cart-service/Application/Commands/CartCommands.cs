using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Security;
using CartService.Domain.Entities;
using CartService.DTOs;
using CartService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CartService.Application.Commands;

/// <summary>加入购物车命令（同 SKU 自动合并数量）</summary>
/// <param name="UserId">买家用户 ID</param>
/// <param name="Request">商品条目信息</param>
public sealed record AddCartItemCommand(Guid UserId, AddCartItemRequest Request) : ICommand<CartItemResponse>;

/// <summary>加入购物车命令处理器</summary>
public sealed class AddCartItemCommandHandler(CartDbContext db) : ICommandHandler<AddCartItemCommand, CartItemResponse>
{
    /// <inheritdoc />
    public async Task<CartItemResponse> HandleAsync(AddCartItemCommand command, CancellationToken ct = default)
    {
        var request = command.Request;

        // 同 SKU 存在则合并数量（上限 999）
        var existing = await db.CartItems.FirstOrDefaultAsync(
            c => c.UserId == command.UserId && c.SkuId == request.SkuId, ct);

        if (existing is not null)
        {
            existing.ChangeQuantity(Math.Min(999, existing.Quantity + request.Quantity));
            await db.SaveChangesAsync(ct);
            return CartMapper.ToResponse(existing);
        }

        var item = new CartItem(command.UserId, request.MerchantId, request.MerchantName, request.ProductId,
            request.ProductName, request.SkuId, request.SkuCode, request.Spec ?? string.Empty,
            request.UnitPrice, request.Quantity);
        db.CartItems.Add(item);
        await db.SaveChangesAsync(ct);
        return CartMapper.ToResponse(item);
    }
}

/// <summary>修改购物车条目数量命令</summary>
/// <param name="UserId">买家用户 ID</param>
/// <param name="ItemId">条目 ID</param>
/// <param name="Quantity">新数量</param>
public sealed record UpdateCartItemQuantityCommand(Guid UserId, Guid ItemId, int Quantity) : ICommand<CartItemResponse>;

/// <summary>修改购物车条目数量命令处理器</summary>
public sealed class UpdateCartItemQuantityCommandHandler(CartDbContext db) : ICommandHandler<UpdateCartItemQuantityCommand, CartItemResponse>
{
    /// <inheritdoc />
    public async Task<CartItemResponse> HandleAsync(UpdateCartItemQuantityCommand command, CancellationToken ct = default)
    {
        var item = await GetOwnedAsync(db, command.UserId, command.ItemId, ct);
        item.ChangeQuantity(command.Quantity);
        await db.SaveChangesAsync(ct);
        return CartMapper.ToResponse(item);
    }

    /// <summary>按归属校验获取条目</summary>
    internal static async Task<CartItem> GetOwnedAsync(CartDbContext db, Guid userId, Guid itemId, CancellationToken ct)
        => await db.CartItems.FirstOrDefaultAsync(c => c.Id == itemId && c.UserId == userId, ct)
           ?? throw new NotFoundException("购物车条目", itemId);
}

/// <summary>设置条目选中状态命令</summary>
/// <param name="UserId">买家用户 ID</param>
/// <param name="ItemId">条目 ID</param>
/// <param name="IsSelected">是否选中</param>
public sealed record SelectCartItemCommand(Guid UserId, Guid ItemId, bool IsSelected) : ICommand<CartItemResponse>;

/// <summary>设置条目选中状态命令处理器</summary>
public sealed class SelectCartItemCommandHandler(CartDbContext db) : ICommandHandler<SelectCartItemCommand, CartItemResponse>
{
    /// <inheritdoc />
    public async Task<CartItemResponse> HandleAsync(SelectCartItemCommand command, CancellationToken ct = default)
    {
        var item = await UpdateCartItemQuantityCommandHandler.GetOwnedAsync(db, command.UserId, command.ItemId, ct);
        item.Select(command.IsSelected);
        await db.SaveChangesAsync(ct);
        return CartMapper.ToResponse(item);
    }
}

/// <summary>删除购物车条目命令</summary>
/// <param name="UserId">买家用户 ID</param>
/// <param name="ItemId">条目 ID</param>
public sealed record RemoveCartItemCommand(Guid UserId, Guid ItemId) : ICommand;

/// <summary>删除购物车条目命令处理器</summary>
public sealed class RemoveCartItemCommandHandler(CartDbContext db) : ICommandHandler<RemoveCartItemCommand>
{
    /// <inheritdoc />
    public async Task<Unit> HandleAsync(RemoveCartItemCommand command, CancellationToken ct = default)
    {
        var item = await UpdateCartItemQuantityCommandHandler.GetOwnedAsync(db, command.UserId, command.ItemId, ct);
        db.CartItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}

/// <summary>清空购物车命令（可只清选中项）</summary>
/// <param name="UserId">买家用户 ID</param>
/// <param name="OnlySelected">是否只清选中项</param>
public sealed record ClearCartCommand(Guid UserId, bool OnlySelected = false) : ICommand;

/// <summary>清空购物车命令处理器</summary>
public sealed class ClearCartCommandHandler(CartDbContext db) : ICommandHandler<ClearCartCommand>
{
    /// <inheritdoc />
    public async Task<Unit> HandleAsync(ClearCartCommand command, CancellationToken ct = default)
    {
        var query = db.CartItems.Where(c => c.UserId == command.UserId);
        if (command.OnlySelected)
            query = query.Where(c => c.IsSelected);

        db.CartItems.RemoveRange(query);
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}

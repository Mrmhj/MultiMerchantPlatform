using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Results;
using SearchService.Domain.Entities;
using SearchService.DTOs;
using SearchService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace SearchService.Application.Commands;

/// <summary>upsert 搜索索引命令（product-service 商品创建/更新/上下架时调用）</summary>
/// <param name="Request">索引数据</param>
public sealed record UpsertSearchIndexCommand(UpsertSearchIndexRequest Request) : ICommand<bool>;

/// <summary>upsert 搜索索引命令处理器</summary>
public sealed class UpsertSearchIndexCommandHandler(SearchDbContext db) : ICommandHandler<UpsertSearchIndexCommand, bool>
{
    /// <inheritdoc />
    public async Task<bool> HandleAsync(UpsertSearchIndexCommand command, CancellationToken ct = default)
    {
        var request = command.Request;
        var existing = await db.Products.FirstOrDefaultAsync(p => p.ProductId == request.ProductId, ct);

        if (existing is null)
        {
            db.Products.Add(new ProductSearchIndex(
                request.ProductId, request.MerchantId, request.MerchantName, request.Name,
                request.Description, request.CategoryId, request.CategoryName, request.CoverImage,
                request.PriceMin, request.PriceMax, request.Status));
        }
        else
        {
            existing.Update(request.MerchantName, request.Name, request.Description, request.CategoryId,
                request.CategoryName, request.CoverImage, request.PriceMin, request.PriceMax, request.Status);
        }

        await db.SaveChangesAsync(ct);
        return true;
    }
}

/// <summary>移除搜索索引命令（商品删除时调用）</summary>
/// <param name="ProductId">商品 ID</param>
public sealed record RemoveSearchIndexCommand(Guid ProductId) : ICommand<bool>;

/// <summary>移除搜索索引命令处理器</summary>
public sealed class RemoveSearchIndexCommandHandler(SearchDbContext db) : ICommandHandler<RemoveSearchIndexCommand, bool>
{
    /// <inheritdoc />
    public async Task<bool> HandleAsync(RemoveSearchIndexCommand command, CancellationToken ct = default)
    {
        var existing = await db.Products.FirstOrDefaultAsync(p => p.ProductId == command.ProductId, ct);
        if (existing is null)
            return false;

        db.Products.Remove(existing);
        await db.SaveChangesAsync(ct);
        return true;
    }
}

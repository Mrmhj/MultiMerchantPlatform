using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.MultiTenant;
using ProductService.Domain.Entities;
using ProductService.Domain.Enums;
using ProductService.DTOs;
using ProductService.Infrastructure;
using ProductService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ProductService.Application.Commands;

/// <summary>创建商品命令（含 SKU 列表，初始状态 Draft）</summary>
public sealed record CreateProductCommand(
    string Name,
    Guid CategoryId,
    string? Description,
    string? CoverImage,
    List<SkuItem> Skus) : ICommand<ProductResponse>;

/// <summary>创建商品命令处理器</summary>
public sealed class CreateProductCommandHandler(
    ProductDbContext db,
    ITenantProvider tenantProvider,
    SearchServiceClient searchClient,
    MerchantServiceClient merchantClient,
    ILogger<CreateProductCommandHandler> logger) : ICommandHandler<CreateProductCommand, ProductResponse>
{
    /// <inheritdoc />
    public async Task<ProductResponse> HandleAsync(CreateProductCommand command, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        // 分类必须属于本商户
        var categoryExists = await db.Categories.AnyAsync(
            c => c.Id == command.CategoryId && c.MerchantId == merchantId, ct);
        if (!categoryExists)
            throw new DomainException("分类不存在或不属于当前商户", "CATEGORY_INVALID");

        var product = new Product(merchantId, command.Name, command.CategoryId, command.Description, command.CoverImage);
        foreach (var sku in command.Skus)
            product.AddSku(sku.SkuCode, sku.Spec, sku.Price, sku.Stock);

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        await SearchIndexSyncHelper.SyncAsync(db, searchClient, merchantClient, product, logger, ct);
        return ProductMapper.ToResponse(product);
    }
}

/// <summary>更新商品命令（基本信息）</summary>
public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    Guid CategoryId,
    string? Description,
    string? CoverImage) : ICommand<ProductResponse>;

/// <summary>更新商品命令处理器</summary>
public sealed class UpdateProductCommandHandler(
    ProductDbContext db,
    ITenantProvider tenantProvider,
    SearchServiceClient searchClient,
    MerchantServiceClient merchantClient,
    ILogger<UpdateProductCommandHandler> logger) : ICommandHandler<UpdateProductCommand, ProductResponse>
{
    /// <inheritdoc />
    public async Task<ProductResponse> HandleAsync(UpdateProductCommand command, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var product = await db.Products
            .Include(p => p.Skus)
            .FirstOrDefaultAsync(p => p.Id == command.Id && p.MerchantId == merchantId, ct)
            ?? throw new NotFoundException("商品", command.Id);

        // 分类校验（若换分类）
        if (product.CategoryId != command.CategoryId)
        {
            var categoryExists = await db.Categories.AnyAsync(
                c => c.Id == command.CategoryId && c.MerchantId == merchantId, ct);
            if (!categoryExists)
                throw new DomainException("分类不存在或不属于当前商户", "CATEGORY_INVALID");
        }

        product.UpdateInfo(command.Name, command.CategoryId, command.Description, command.CoverImage);
        await db.SaveChangesAsync(ct);

        await SearchIndexSyncHelper.SyncAsync(db, searchClient, merchantClient, product, logger, ct);
        return ProductMapper.ToResponse(product);
    }
}

/// <summary>商品上下架命令</summary>
public sealed record UpdateProductStatusCommand(Guid Id, ProductStatus Status) : ICommand<ProductResponse>;

/// <summary>商品上下架命令处理器</summary>
public sealed class UpdateProductStatusCommandHandler(
    ProductDbContext db,
    ITenantProvider tenantProvider,
    SearchServiceClient searchClient,
    MerchantServiceClient merchantClient,
    ILogger<UpdateProductStatusCommandHandler> logger) : ICommandHandler<UpdateProductStatusCommand, ProductResponse>
{
    /// <inheritdoc />
    public async Task<ProductResponse> HandleAsync(UpdateProductStatusCommand command, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var product = await db.Products
            .Include(p => p.Skus)
            .FirstOrDefaultAsync(p => p.Id == command.Id && p.MerchantId == merchantId, ct)
            ?? throw new NotFoundException("商品", command.Id);

        switch (command.Status)
        {
            case ProductStatus.OnSale:
                product.Publish(); // 无启用 SKU 时抛 InvalidOperationException
                break;
            case ProductStatus.OffSale:
                product.Unpublish();
                break;
            case ProductStatus.Draft:
                throw new DomainException("商品不能直接改回草稿状态", "INVALID_STATUS");
            default:
                throw new DomainException("不支持的上下架状态", "INVALID_STATUS");
        }

        await db.SaveChangesAsync(ct);

        await SearchIndexSyncHelper.SyncAsync(db, searchClient, merchantClient, product, logger, ct);
        return ProductMapper.ToResponse(product);
    }
}

/// <summary>
/// 搜索索引同步辅助 — 商品变更后同步到 search-service（失败仅记日志，不阻塞主流程）。
/// </summary>
internal static class SearchIndexSyncHelper
{
    /// <summary>同步商品到搜索索引</summary>
    /// <param name="db">商品数据库上下文</param>
    /// <param name="searchClient">搜索服务客户端</param>
    /// <param name="merchantClient">商户服务客户端（查商户名）</param>
    /// <param name="product">商品实体</param>
    /// <param name="logger">日志</param>
    /// <param name="ct">取消令牌</param>
    public static async Task SyncAsync(ProductDbContext db, SearchServiceClient searchClient,
        MerchantServiceClient merchantClient, Product product, ILogger logger, CancellationToken ct)
    {
        try
        {
            // 分类名（本库直查）与商户名（跨服务查询）
            var categoryName = await db.Categories.AsNoTracking()
                .Where(c => c.Id == product.CategoryId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(ct) ?? "未分类";
            var merchantName = await merchantClient.GetNameAsync(product.MerchantId, ct)
                ?? $"商户-{product.MerchantId:N}"[..16];

            // 价格区间（SKU 最小/最大价）
            var prices = product.Skus.Select(s => s.Price).ToList();
            var priceMin = prices.Count > 0 ? prices.Min() : 0;
            var priceMax = prices.Count > 0 ? prices.Max() : 0;

            await searchClient.UpsertAsync(product.Id, product.MerchantId, merchantName, product.Name,
                product.Description, product.CategoryId, categoryName, product.CoverImage,
                priceMin, priceMax, (int)product.Status, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "搜索索引同步失败 ProductId={ProductId}", product.Id);
        }
    }
}

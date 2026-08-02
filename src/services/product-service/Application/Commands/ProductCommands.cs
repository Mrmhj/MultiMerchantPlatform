using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.MultiTenant;
using ProductService.Domain.Entities;
using ProductService.Domain.Enums;
using ProductService.DTOs;
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
    ITenantProvider tenantProvider) : ICommandHandler<CreateProductCommand, ProductResponse>
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
    ITenantProvider tenantProvider) : ICommandHandler<UpdateProductCommand, ProductResponse>
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

        return ProductMapper.ToResponse(product);
    }
}

/// <summary>商品上下架命令</summary>
public sealed record UpdateProductStatusCommand(Guid Id, ProductStatus Status) : ICommand<ProductResponse>;

/// <summary>商品上下架命令处理器</summary>
public sealed class UpdateProductStatusCommandHandler(
    ProductDbContext db,
    ITenantProvider tenantProvider) : ICommandHandler<UpdateProductStatusCommand, ProductResponse>
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
        return ProductMapper.ToResponse(product);
    }
}

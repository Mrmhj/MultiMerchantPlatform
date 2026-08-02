using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.MultiTenant;
using ProductService.Domain.Entities;
using ProductService.DTOs;
using ProductService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ProductService.Application.Commands;

/// <summary>创建分类命令</summary>
public sealed record CreateCategoryCommand(string Name, Guid? ParentId, int SortOrder) : ICommand<CategoryResponse>;

/// <summary>创建分类命令处理器</summary>
public sealed class CreateCategoryCommandHandler(
    ProductDbContext db,
    ITenantProvider tenantProvider) : ICommandHandler<CreateCategoryCommand, CategoryResponse>
{
    /// <inheritdoc />
    public async Task<CategoryResponse> HandleAsync(CreateCategoryCommand command, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        // 同商户下同级分类名称唯一
        var exists = await db.Categories.AnyAsync(
            c => c.MerchantId == merchantId && c.ParentId == command.ParentId && c.Name == command.Name.Trim(), ct);
        if (exists)
            throw new DomainException("同级分类名称已存在", "NAME_EXISTS");

        var category = new Category(merchantId, command.Name, command.ParentId, command.SortOrder);
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);

        return CategoryMapper.ToResponse(category);
    }
}

/// <summary>更新分类命令</summary>
public sealed record UpdateCategoryCommand(Guid Id, string Name, Guid? ParentId, int SortOrder, bool IsActive) : ICommand<CategoryResponse>;

/// <summary>更新分类命令处理器</summary>
public sealed class UpdateCategoryCommandHandler(
    ProductDbContext db,
    ITenantProvider tenantProvider) : ICommandHandler<UpdateCategoryCommand, CategoryResponse>
{
    /// <inheritdoc />
    public async Task<CategoryResponse> HandleAsync(UpdateCategoryCommand command, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var category = await db.Categories.FirstOrDefaultAsync(
            c => c.Id == command.Id && c.MerchantId == merchantId, ct)
            ?? throw new NotFoundException("分类", command.Id);

        category.Update(command.Name, command.ParentId, command.SortOrder);
        if (command.IsActive)
            category.Activate();
        else
            category.Deactivate();

        await db.SaveChangesAsync(ct);
        return CategoryMapper.ToResponse(category);
    }
}

/// <summary>删除分类命令（有商品或子分类时禁止删除）</summary>
public sealed record DeleteCategoryCommand(Guid Id) : ICommand;

/// <summary>删除分类命令处理器</summary>
public sealed class DeleteCategoryCommandHandler(
    ProductDbContext db,
    ITenantProvider tenantProvider) : ICommandHandler<DeleteCategoryCommand>
{
    /// <inheritdoc />
    public async Task<Unit> HandleAsync(DeleteCategoryCommand command, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var category = await db.Categories.FirstOrDefaultAsync(
            c => c.Id == command.Id && c.MerchantId == merchantId, ct)
            ?? throw new NotFoundException("分类", command.Id);

        var hasChildren = await db.Categories.AnyAsync(c => c.ParentId == command.Id, ct);
        if (hasChildren)
            throw new DomainException("该分类下存在子分类，无法删除", "HAS_CHILDREN");

        var hasProducts = await db.Products.AnyAsync(p => p.CategoryId == command.Id, ct);
        if (hasProducts)
            throw new DomainException("该分类下存在商品，无法删除", "HAS_PRODUCTS");

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}

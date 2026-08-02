using ProductService.Domain.Entities;
using ProductService.DTOs;

namespace ProductService.Application;

/// <summary>
/// 分类实体 → DTO 映射。
/// </summary>
public static class CategoryMapper
{
    /// <summary>分类实体转响应 DTO</summary>
    /// <param name="category">分类实体</param>
    /// <returns>分类响应</returns>
    public static CategoryResponse ToResponse(Category category) => new()
    {
        Id = category.Id,
        MerchantId = category.MerchantId,
        Name = category.Name,
        ParentId = category.ParentId,
        SortOrder = category.SortOrder,
        IsActive = category.IsActive,
        CreatedAt = category.CreatedAt,
    };
}

/// <summary>
/// 商品实体 → DTO 映射。
/// </summary>
public static class ProductMapper
{
    /// <summary>SKU 实体转响应 DTO</summary>
    /// <param name="sku">SKU 实体</param>
    /// <returns>SKU 响应</returns>
    public static ProductSkuResponse ToSkuResponse(ProductSku sku) => new()
    {
        Id = sku.Id,
        SkuCode = sku.SkuCode,
        Spec = sku.Spec,
        Price = sku.Price,
        Stock = sku.Stock,
        IsActive = sku.IsActive,
    };

    /// <summary>商品实体转响应 DTO（含 SKU 列表）</summary>
    /// <param name="product">商品实体</param>
    /// <returns>商品响应</returns>
    public static ProductResponse ToResponse(Product product) => new()
    {
        Id = product.Id,
        MerchantId = product.MerchantId,
        Name = product.Name,
        CategoryId = product.CategoryId,
        Description = product.Description,
        CoverImage = product.CoverImage,
        Status = product.Status,
        Skus = product.Skus.Select(ToSkuResponse).ToList(),
        CreatedAt = product.CreatedAt,
    };
}

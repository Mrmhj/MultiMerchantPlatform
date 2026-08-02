using SearchService.Domain.Entities;
using SearchService.DTOs;

namespace SearchService.Application;

/// <summary>
/// 实体 → DTO 映射。
/// </summary>
public static class SearchMapper
{
    /// <summary>索引实体转搜索结果 DTO</summary>
    /// <param name="entity">索引实体</param>
    /// <returns>搜索结果条目</returns>
    public static SearchResultItem ToResponse(ProductSearchIndex entity) => new()
    {
        Id = entity.Id,
        ProductId = entity.ProductId,
        MerchantId = entity.MerchantId,
        MerchantName = entity.MerchantName,
        Name = entity.Name,
        Description = entity.Description,
        CategoryId = entity.CategoryId,
        CategoryName = entity.CategoryName,
        CoverImage = entity.CoverImage,
        PriceMin = entity.PriceMin,
        PriceMax = entity.PriceMax,
        UpdatedAt = entity.UpdatedAt ?? entity.CreatedAt,
    };
}

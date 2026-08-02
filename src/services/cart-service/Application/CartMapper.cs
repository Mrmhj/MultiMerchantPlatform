using CartService.Domain.Entities;
using CartService.DTOs;

namespace CartService.Application;

/// <summary>
/// 实体 → DTO 映射。
/// </summary>
public static class CartMapper
{
    /// <summary>购物车条目实体转响应 DTO</summary>
    /// <param name="item">条目实体</param>
    /// <returns>条目响应</returns>
    public static CartItemResponse ToResponse(CartItem item) => new()
    {
        Id = item.Id,
        MerchantId = item.MerchantId,
        MerchantName = item.MerchantName,
        ProductId = item.ProductId,
        ProductName = item.ProductName,
        SkuId = item.SkuId,
        SkuCode = item.SkuCode,
        Spec = item.Spec,
        UnitPrice = item.UnitPrice,
        Quantity = item.Quantity,
        IsSelected = item.IsSelected,
        CreatedAt = item.CreatedAt,
    };
}

using StockService.Domain.Entities;
using StockService.DTOs;

namespace StockService.Application;

/// <summary>
/// 实体 → DTO 映射。
/// </summary>
public static class StockMapper
{
    /// <summary>库存实体转响应 DTO</summary>
    /// <param name="item">库存实体</param>
    /// <returns>库存响应</returns>
    public static StockResponse ToResponse(StockItem item) => new()
    {
        SkuId = item.SkuId,
        MerchantId = item.MerchantId,
        Total = item.Total,
        Reserved = item.Reserved,
        Available = item.Available,
    };

    /// <summary>流水实体转响应 DTO</summary>
    /// <param name="tx">流水实体</param>
    /// <returns>流水响应</returns>
    public static StockTransactionResponse ToTransactionResponse(StockTransaction tx) => new()
    {
        Id = tx.Id,
        SkuId = tx.SkuId,
        Type = tx.Type,
        Quantity = tx.Quantity,
        ReferenceId = tx.ReferenceId,
        CreatedAt = tx.CreatedAt,
    };
}

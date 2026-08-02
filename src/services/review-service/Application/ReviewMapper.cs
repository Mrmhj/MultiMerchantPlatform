using ReviewService.Domain.Entities;
using ReviewService.DTOs;

namespace ReviewService.Application;

/// <summary>
/// 实体 → DTO 映射。
/// </summary>
public static class ReviewMapper
{
    /// <summary>评价实体转响应 DTO</summary>
    /// <param name="review">评价实体</param>
    /// <returns>评价响应</returns>
    public static ReviewResponse ToResponse(Review review) => new()
    {
        Id = review.Id,
        MerchantId = review.MerchantId,
        ProductId = review.ProductId,
        ProductName = review.ProductName,
        SkuSpec = review.SkuSpec,
        Rating = review.Rating,
        Content = review.Content,
        IsAnonymous = review.IsAnonymous,
        DisplayName = review.IsAnonymous ? "匿名用户" : null,
        UserId = review.UserId,
        Status = review.Status,
        ReplyContent = review.ReplyContent,
        RepliedAt = review.RepliedAt,
        CreatedAt = review.CreatedAt,
    };
}

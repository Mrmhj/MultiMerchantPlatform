using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;
using ReviewService.Domain.Enums;

namespace ReviewService.Domain.Entities;

/// <summary>
/// 商品评价 — 买家维度归属（UserId），关联订单/商品；商户维度（MerchantId）多租户隔离。
/// 同一用户对同一订单项只能评价一次（唯一索引：UserId + SubOrderId + ProductId）。
/// 商户可回复（Reply）与隐藏（Hide/Show）。
/// </summary>
public sealed class Review : MultiTenantEntity
{
    private Review() { } // EF Core

    /// <summary>创建评价</summary>
    /// <param name="userId">买家用户 ID（评价人）</param>
    /// <param name="merchantId">所属商户 ID</param>
    /// <param name="orderId">主订单 ID</param>
    /// <param name="subOrderId">子订单 ID（唯一约束维度）</param>
    /// <param name="productId">商品 ID</param>
    /// <param name="productName">商品名称（快照）</param>
    /// <param name="skuId">SKU ID</param>
    /// <param name="skuSpec">SKU 规格（快照）</param>
    /// <param name="rating">评分（1-5）</param>
    /// <param name="content">评价内容（1-500 字）</param>
    /// <param name="isAnonymous">是否匿名展示</param>
    [SetsRequiredMembers]
    public Review(Guid userId, Guid merchantId, Guid orderId, Guid subOrderId,
        Guid productId, string productName, Guid skuId, string skuSpec,
        int rating, string content, bool isAnonymous)
    {
        UserId = userId;
        MerchantId = merchantId;
        OrderId = orderId;
        SubOrderId = subOrderId;
        ProductId = productId;
        ChangeSnapshot(productName, skuSpec);
        SkuId = skuId;
        ChangeRating(rating);
        ChangeContent(content);
        IsAnonymous = isAnonymous;
    }

    /// <summary>买家用户 ID（评价人，隔离维度）</summary>
    public Guid UserId { get; private set; }

    /// <summary>主订单 ID</summary>
    public Guid OrderId { get; private set; }

    /// <summary>子订单 ID（同一子订单项仅允许一条评价）</summary>
    public Guid SubOrderId { get; private set; }

    /// <summary>商品 ID</summary>
    public Guid ProductId { get; private set; }

    /// <summary>商品名称（快照）</summary>
    public string ProductName { get; private set; } = string.Empty;

    /// <summary>SKU ID</summary>
    public Guid SkuId { get; private set; }

    /// <summary>SKU 规格（快照）</summary>
    public string SkuSpec { get; private set; } = string.Empty;

    /// <summary>评分（1-5）</summary>
    public int Rating { get; private set; }

    /// <summary>评价内容（1-500 字）</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>是否匿名展示</summary>
    public bool IsAnonymous { get; private set; }

    /// <summary>状态（Visible/Hidden）</summary>
    public ReviewStatus Status { get; private set; } = ReviewStatus.Visible;

    /// <summary>商户回复内容（未回复为 null）</summary>
    public string? ReplyContent { get; private set; }

    /// <summary>商户回复时间（未回复为 null）</summary>
    public DateTime? RepliedAt { get; private set; }

    /// <summary>修改商品快照字段</summary>
    /// <param name="productName">商品名称（1-200 字）</param>
    /// <param name="skuSpec">SKU 规格（1-100 字）</param>
    public void ChangeSnapshot(string productName, string skuSpec)
    {
        if (string.IsNullOrWhiteSpace(productName) || productName.Length > 200)
            throw new DomainException("商品名称非法", "INVALID_PRODUCT_NAME");
        ProductName = productName.Trim();
        SkuSpec = (skuSpec ?? string.Empty).Trim();
    }

    /// <summary>修改评分</summary>
    /// <param name="rating">评分（1-5）</param>
    public void ChangeRating(int rating)
    {
        if (rating is < 1 or > 5)
            throw new DomainException("评分需在 1-5 星之间", "INVALID_RATING");
        Rating = rating;
    }

    /// <summary>修改评价内容</summary>
    /// <param name="content">内容（1-500 字）</param>
    public void ChangeContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > 500)
            throw new DomainException("评价内容需在 1-500 字之间", "INVALID_CONTENT");
        Content = content.Trim();
    }

    /// <summary>设置匿名标志</summary>
    /// <param name="isAnonymous">是否匿名</param>
    public void SetAnonymous(bool isAnonymous) => IsAnonymous = isAnonymous;

    /// <summary>商户回复（未回复或想修改回复时可调用）</summary>
    /// <param name="reply">回复内容（1-500 字）</param>
    /// <param name="now">回复时间（UTC）</param>
    public void Reply(string reply, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(reply) || reply.Length > 500)
            throw new DomainException("回复内容需在 1-500 字之间", "INVALID_REPLY");
        ReplyContent = reply.Trim();
        RepliedAt = now;
    }

    /// <summary>隐藏（违规评价下架，C 端不再展示）</summary>
    public void Hide()
    {
        if (Status == ReviewStatus.Hidden)
            throw new DomainException("评价已处于隐藏状态", "REVIEW_ALREADY_HIDDEN");
        Status = ReviewStatus.Hidden;
    }

    /// <summary>恢复可见</summary>
    public void Show()
    {
        if (Status == ReviewStatus.Visible)
            throw new DomainException("评价已处于可见状态", "REVIEW_ALREADY_VISIBLE");
        Status = ReviewStatus.Visible;
    }
}

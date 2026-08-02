using System.ComponentModel.DataAnnotations;
using ReviewService.Domain.Enums;

namespace ReviewService.DTOs;

/// <summary>创建评价请求（买家端）</summary>
public sealed record CreateReviewRequest
{
    /// <summary>所属商户 ID</summary>
    [Required]
    public Guid MerchantId { get; init; }

    /// <summary>主订单 ID</summary>
    [Required]
    public Guid OrderId { get; init; }

    /// <summary>子订单 ID（同一子订单项仅允许一条评价）</summary>
    [Required]
    public Guid SubOrderId { get; init; }

    /// <summary>商品 ID</summary>
    [Required]
    public Guid ProductId { get; init; }

    /// <summary>商品名称</summary>
    [Required, StringLength(200)]
    public required string ProductName { get; init; }

    /// <summary>SKU ID</summary>
    [Required]
    public Guid SkuId { get; init; }

    /// <summary>SKU 规格</summary>
    [StringLength(100)]
    public string? SkuSpec { get; init; }

    /// <summary>评分（1-5）</summary>
    [Range(1, 5)]
    public int Rating { get; init; }

    /// <summary>评价内容（1-500 字）</summary>
    [Required, StringLength(500, MinimumLength = 1)]
    public required string Content { get; init; }

    /// <summary>是否匿名展示</summary>
    public bool IsAnonymous { get; init; }
}

/// <summary>商户回复评价请求</summary>
public sealed record ReplyReviewRequest
{
    /// <summary>回复内容（1-500 字）</summary>
    [Required, StringLength(500, MinimumLength = 1)]
    public required string Reply { get; init; }
}

/// <summary>评价状态变更请求（商户端）</summary>
public sealed record ChangeReviewStatusRequest
{
    /// <summary>目标状态：可见 true / 隐藏 false</summary>
    [Required]
    public bool Visible { get; init; }
}

/// <summary>评价响应（买家/商户/C 端通用）</summary>
public sealed record ReviewResponse
{
    /// <summary>评价 ID</summary>
    public Guid Id { get; init; }

    /// <summary>所属商户 ID</summary>
    public Guid MerchantId { get; init; }

    /// <summary>商品 ID</summary>
    public Guid ProductId { get; init; }

    /// <summary>商品名称（快照）</summary>
    public string ProductName { get; init; } = string.Empty;

    /// <summary>SKU 规格（快照）</summary>
    public string SkuSpec { get; init; } = string.Empty;

    /// <summary>评分（1-5）</summary>
    public int Rating { get; init; }

    /// <summary>评价内容</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>是否匿名（买家视角返回原始值；C 端公开展示时匿名显示为「匿名用户」）</summary>
    public bool IsAnonymous { get; init; }

    /// <summary>展示用户名（匿名时为「匿名用户」，需买家/商户端自行替换）</summary>
    public string? DisplayName { get; init; }

    /// <summary>评价人用户 ID（买家/商户端可见）</summary>
    public Guid? UserId { get; init; }

    /// <summary>状态（Visible/Hidden）</summary>
    public ReviewStatus Status { get; init; }

    /// <summary>商户回复内容（未回复为 null）</summary>
    public string? ReplyContent { get; init; }

    /// <summary>商户回复时间（未回复为 null）</summary>
    public DateTime? RepliedAt { get; init; }

    /// <summary>评价时间</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>商品评价列表响应（C 端公开，含评分统计）</summary>
public sealed record ProductReviewsResponse
{
    /// <summary>商品 ID</summary>
    public Guid ProductId { get; init; }

    /// <summary>平均评分（保留 1 位小数，无评价为 0）</summary>
    public decimal AverageRating { get; init; }

    /// <summary>评价总数（仅可见）</summary>
    public int TotalCount { get; init; }

    /// <summary>评分分布（key=星数 1-5，value=数量）</summary>
    public Dictionary<int, int> RatingDistribution { get; init; } = [];

    /// <summary>评价列表（分页）</summary>
    public List<ReviewResponse> Items { get; init; } = [];
}

using System.ComponentModel.DataAnnotations;

namespace SearchService.DTOs;

/// <summary>搜索索引 upsert 请求（product-service 内部调用）</summary>
public sealed record UpsertSearchIndexRequest
{
    /// <summary>商品 ID</summary>
    [Required]
    public Guid ProductId { get; init; }

    /// <summary>所属商户 ID</summary>
    [Required]
    public Guid MerchantId { get; init; }

    /// <summary>商户名称</summary>
    [Required, StringLength(100)]
    public required string MerchantName { get; init; }

    /// <summary>商品名称</summary>
    [Required, StringLength(200)]
    public required string Name { get; init; }

    /// <summary>商品描述</summary>
    [StringLength(2000)]
    public string? Description { get; init; }

    /// <summary>分类 ID</summary>
    [Required]
    public Guid CategoryId { get; init; }

    /// <summary>分类名称</summary>
    [Required, StringLength(50)]
    public required string CategoryName { get; init; }

    /// <summary>封面图 URL</summary>
    [StringLength(500)]
    public string? CoverImage { get; init; }

    /// <summary>最低 SKU 价</summary>
    [Range(0, 9999999)]
    public decimal PriceMin { get; init; }

    /// <summary>最高 SKU 价</summary>
    [Range(0, 9999999)]
    public decimal PriceMax { get; init; }

    /// <summary>商品状态（2=在售）</summary>
    public int Status { get; init; }
}

/// <summary>搜索索引移除请求（product-service 内部调用）</summary>
public sealed record RemoveSearchIndexRequest
{
    /// <summary>商品 ID</summary>
    [Required]
    public Guid ProductId { get; init; }
}

/// <summary>搜索结果条目</summary>
public sealed record SearchResultItem
{
    /// <summary>索引记录 ID</summary>
    public Guid Id { get; init; }

    /// <summary>商品 ID</summary>
    public Guid ProductId { get; init; }

    /// <summary>所属商户 ID</summary>
    public Guid MerchantId { get; init; }

    /// <summary>商户名称</summary>
    public string MerchantName { get; init; } = string.Empty;

    /// <summary>商品名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>商品描述</summary>
    public string? Description { get; init; }

    /// <summary>分类 ID</summary>
    public Guid CategoryId { get; init; }

    /// <summary>分类名称</summary>
    public string CategoryName { get; init; } = string.Empty;

    /// <summary>封面图 URL</summary>
    public string? CoverImage { get; init; }

    /// <summary>最低 SKU 价</summary>
    public decimal PriceMin { get; init; }

    /// <summary>最高 SKU 价</summary>
    public decimal PriceMax { get; init; }

    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; init; }
}

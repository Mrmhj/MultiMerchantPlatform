using System.ComponentModel.DataAnnotations;
using ProductService.Domain.Enums;

namespace ProductService.DTOs;

/// <summary>分类创建/更新请求</summary>
public sealed record CategoryRequest
{
    /// <summary>分类名称</summary>
    [Required, StringLength(50, MinimumLength = 1)]
    public required string Name { get; init; }

    /// <summary>父分类 ID（可选，空为顶级）</summary>
    public Guid? ParentId { get; init; }

    /// <summary>排序值（小在前，默认 0）</summary>
    public int SortOrder { get; init; }

    /// <summary>是否启用（更新时可选，缺省 true）</summary>
    public bool? IsActive { get; init; }
}

/// <summary>分类响应</summary>
public sealed record CategoryResponse
{
    /// <summary>分类 ID</summary>
    public Guid Id { get; init; }

    /// <summary>所属商户 ID</summary>
    public Guid MerchantId { get; init; }

    /// <summary>分类名称</summary>
    public required string Name { get; init; }

    /// <summary>父分类 ID</summary>
    public Guid? ParentId { get; init; }

    /// <summary>排序值</summary>
    public int SortOrder { get; init; }

    /// <summary>是否启用</summary>
    public bool IsActive { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>SKU 项（创建商品时随附）</summary>
public sealed record SkuItem
{
    /// <summary>SKU 编码（商户内唯一，如 500G001）</summary>
    [Required, StringLength(50, MinimumLength = 1)]
    public required string SkuCode { get; init; }

    /// <summary>规格描述（如 500g / 1kg）</summary>
    [Required, StringLength(100, MinimumLength = 1)]
    public required string Spec { get; init; }

    /// <summary>售价（元，≥0）</summary>
    [Range(0, 99999999)]
    public decimal Price { get; init; }

    /// <summary>初始库存（≥0）</summary>
    [Range(0, 9999999)]
    public int Stock { get; init; }
}

/// <summary>商品创建请求</summary>
public sealed record CreateProductRequest
{
    /// <summary>商品名称</summary>
    [Required, StringLength(200, MinimumLength = 1)]
    public required string Name { get; init; }

    /// <summary>分类 ID</summary>
    [Required]
    public Guid CategoryId { get; init; }

    /// <summary>商品描述（可选）</summary>
    [StringLength(2000)]
    public string? Description { get; init; }

    /// <summary>封面图 URL（可选）</summary>
    [StringLength(500)]
    public string? CoverImage { get; init; }

    /// <summary>SKU 列表（至少一个）</summary>
    [Required, MinLength(1)]
    public List<SkuItem> Skus { get; init; } = [];
}

/// <summary>商品更新请求</summary>
public sealed record UpdateProductRequest
{
    /// <summary>商品名称</summary>
    [Required, StringLength(200, MinimumLength = 1)]
    public required string Name { get; init; }

    /// <summary>分类 ID</summary>
    [Required]
    public Guid CategoryId { get; init; }

    /// <summary>商品描述（可选）</summary>
    [StringLength(2000)]
    public string? Description { get; init; }

    /// <summary>封面图 URL（可选）</summary>
    [StringLength(500)]
    public string? CoverImage { get; init; }
}

/// <summary>商品上下架请求</summary>
public sealed record UpdateProductStatusRequest
{
    /// <summary>目标状态（2=上架 3=下架）</summary>
    [Required]
    public ProductStatus Status { get; init; }
}

/// <summary>商品 SKU 响应</summary>
public sealed record ProductSkuResponse
{
    /// <summary>SKU ID</summary>
    public Guid Id { get; init; }

    /// <summary>SKU 编码</summary>
    public required string SkuCode { get; init; }

    /// <summary>规格描述</summary>
    public required string Spec { get; init; }

    /// <summary>售价（元）</summary>
    public decimal Price { get; init; }

    /// <summary>库存数量</summary>
    public int Stock { get; init; }

    /// <summary>是否启用</summary>
    public bool IsActive { get; init; }
}

/// <summary>商品响应</summary>
public sealed record ProductResponse
{
    /// <summary>商品 ID</summary>
    public Guid Id { get; init; }

    /// <summary>所属商户 ID</summary>
    public Guid MerchantId { get; init; }

    /// <summary>商品名称</summary>
    public required string Name { get; init; }

    /// <summary>分类 ID</summary>
    public Guid CategoryId { get; init; }

    /// <summary>商品描述</summary>
    public string? Description { get; init; }

    /// <summary>封面图 URL</summary>
    public string? CoverImage { get; init; }

    /// <summary>商品状态（1=草稿 2=在售 3=下架）</summary>
    public ProductStatus Status { get; init; }

    /// <summary>SKU 列表</summary>
    public required List<ProductSkuResponse> Skus { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

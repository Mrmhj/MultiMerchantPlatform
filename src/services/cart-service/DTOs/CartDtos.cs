using System.ComponentModel.DataAnnotations;

namespace CartService.DTOs;

/// <summary>加入购物车请求</summary>
public sealed record AddCartItemRequest
{
    /// <summary>所属商户 ID</summary>
    [Required]
    public Guid MerchantId { get; init; }

    /// <summary>商户名称</summary>
    [Required, StringLength(100)]
    public required string MerchantName { get; init; }

    /// <summary>商品 ID</summary>
    [Required]
    public Guid ProductId { get; init; }

    /// <summary>商品名称</summary>
    [Required, StringLength(200)]
    public required string ProductName { get; init; }

    /// <summary>SKU ID</summary>
    [Required]
    public Guid SkuId { get; init; }

    /// <summary>SKU 编码</summary>
    [Required, StringLength(50)]
    public required string SkuCode { get; init; }

    /// <summary>规格描述</summary>
    [StringLength(100)]
    public string? Spec { get; init; }

    /// <summary>单价</summary>
    [Range(0.01, 9999999)]
    public decimal UnitPrice { get; init; }

    /// <summary>数量（1-999）</summary>
    [Range(1, 999)]
    public int Quantity { get; init; } = 1;
}

/// <summary>改数量请求</summary>
public sealed record UpdateQuantityRequest
{
    /// <summary>新数量（1-999）</summary>
    [Range(1, 999)]
    public int Quantity { get; init; }
}

/// <summary>选中状态请求</summary>
public sealed record SelectRequest
{
    /// <summary>是否选中</summary>
    public bool IsSelected { get; init; } = true;
}

/// <summary>购物车条目响应</summary>
public sealed record CartItemResponse
{
    /// <summary>条目 ID</summary>
    public Guid Id { get; init; }

    /// <summary>所属商户 ID</summary>
    public Guid MerchantId { get; init; }

    /// <summary>商户名称</summary>
    public string MerchantName { get; init; } = string.Empty;

    /// <summary>商品 ID</summary>
    public Guid ProductId { get; init; }

    /// <summary>商品名称</summary>
    public string ProductName { get; init; } = string.Empty;

    /// <summary>SKU ID</summary>
    public Guid SkuId { get; init; }

    /// <summary>SKU 编码</summary>
    public string SkuCode { get; init; } = string.Empty;

    /// <summary>规格描述</summary>
    public string Spec { get; init; } = string.Empty;

    /// <summary>单价</summary>
    public decimal UnitPrice { get; init; }

    /// <summary>数量</summary>
    public int Quantity { get; init; }

    /// <summary>小计（单价×数量）</summary>
    public decimal Subtotal => UnitPrice * Quantity;

    /// <summary>是否选中</summary>
    public bool IsSelected { get; init; }

    /// <summary>加入时间</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>购物车响应（买家维度）</summary>
public sealed record CartResponse
{
    /// <summary>条目列表（按商户分组排序）</summary>
    public List<CartItemResponse> Items { get; init; } = [];

    /// <summary>条目总数</summary>
    public int TotalCount { get; init; }

    /// <summary>选中条目数</summary>
    public int SelectedCount { get; init; }

    /// <summary>选中合计金额</summary>
    public decimal SelectedTotal { get; init; }
}

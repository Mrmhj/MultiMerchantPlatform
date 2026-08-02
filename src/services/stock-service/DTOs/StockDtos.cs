using System.ComponentModel.DataAnnotations;
using StockService.Domain.Enums;

namespace StockService.DTOs;

/// <summary>创建库存请求（商户）</summary>
public sealed record CreateStockRequest
{
    /// <summary>SKU ID（全局唯一）</summary>
    [Required]
    public Guid SkuId { get; init; }

    /// <summary>初始总库存</summary>
    [Range(0, 9999999)]
    public int Total { get; init; }
}

/// <summary>补货请求（商户）</summary>
public sealed record IncreaseStockRequest
{
    /// <summary>补货数量（>0）</summary>
    [Range(1, 9999999)]
    public int Quantity { get; init; }
}

/// <summary>内部库存操作请求（订单/支付回调）</summary>
public sealed record InternalStockRequest
{
    /// <summary>SKU ID</summary>
    [Required]
    public Guid SkuId { get; init; }

    /// <summary>数量（>0）</summary>
    [Range(1, 999999)]
    public int Quantity { get; init; }

    /// <summary>关联业务号（订单号，可选）</summary>
    [StringLength(64)]
    public string? ReferenceId { get; init; }
}

/// <summary>库存响应</summary>
public sealed record StockResponse
{
    /// <summary>SKU ID</summary>
    public Guid SkuId { get; init; }

    /// <summary>商户 ID</summary>
    public Guid MerchantId { get; init; }

    /// <summary>总库存</summary>
    public int Total { get; init; }

    /// <summary>已预占</summary>
    public int Reserved { get; init; }

    /// <summary>可用库存（总-预占）</summary>
    public int Available { get; init; }
}

/// <summary>库存流水响应</summary>
public sealed record StockTransactionResponse
{
    /// <summary>流水 ID</summary>
    public Guid Id { get; init; }

    /// <summary>SKU ID</summary>
    public Guid SkuId { get; init; }

    /// <summary>流水类型（1创建 2预占 3扣减 4释放 5补货）</summary>
    public StockTransactionType Type { get; init; }

    /// <summary>变动数量</summary>
    public int Quantity { get; init; }

    /// <summary>关联业务号</summary>
    public string? ReferenceId { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

using System.ComponentModel.DataAnnotations;
using PromotionService.Domain.Enums;

namespace PromotionService.DTOs;

/// <summary>创建秒杀活动请求（商户端）</summary>
public sealed record CreateSeckillRequest
{
    /// <summary>活动名称（1-100 字）</summary>
    [Required, StringLength(100)]
    public required string Name { get; init; }

    /// <summary>商户名称（快照，1-100 字）</summary>
    [Required, StringLength(100)]
    public required string MerchantName { get; init; }

    /// <summary>商品 ID</summary>
    [Required]
    public Guid ProductId { get; init; }

    /// <summary>商品名称（快照，1-200 字）</summary>
    [Required, StringLength(200)]
    public required string ProductName { get; init; }

    /// <summary>SKU ID</summary>
    [Required]
    public Guid SkuId { get; init; }

    /// <summary>SKU 编码（快照，1-50 字）</summary>
    [Required, StringLength(50)]
    public required string SkuCode { get; init; }

    /// <summary>规格（快照，如 500g）</summary>
    [StringLength(100)]
    public string Spec { get; init; } = string.Empty;

    /// <summary>秒杀价（&gt; 0）</summary>
    [Range(0.01, 99999999)]
    public decimal SeckillPrice { get; init; }

    /// <summary>秒杀总库存（1-999999）</summary>
    [Range(1, 999999)]
    public int TotalStock { get; init; }

    /// <summary>每人限购数量（1-999）</summary>
    [Range(1, 999)]
    public int LimitPerUser { get; init; } = 1;

    /// <summary>开始时间（UTC）</summary>
    [Required]
    public DateTime StartTime { get; init; }

    /// <summary>结束时间（UTC）</summary>
    [Required]
    public DateTime EndTime { get; init; }
}

/// <summary>秒杀活动状态变更请求</summary>
public sealed record ChangeSeckillStatusRequest
{
    /// <summary>目标状态：启用 true / 停用 false</summary>
    [Required]
    public bool Active { get; init; }
}

/// <summary>秒杀抢购请求（买家端）</summary>
public sealed record BuySeckillRequest
{
    /// <summary>购买数量（1-每人限购）</summary>
    [Range(1, 999)]
    public int Quantity { get; init; } = 1;
}

/// <summary>秒杀活动响应</summary>
public sealed record SeckillResponse
{
    /// <summary>活动 ID</summary>
    public Guid Id { get; init; }

    /// <summary>所属商户 ID</summary>
    public Guid MerchantId { get; init; }

    /// <summary>活动名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>商户名称（快照）</summary>
    public string MerchantName { get; init; } = string.Empty;

    /// <summary>商品 ID</summary>
    public Guid ProductId { get; init; }

    /// <summary>商品名称</summary>
    public string ProductName { get; init; } = string.Empty;

    /// <summary>SKU ID</summary>
    public Guid SkuId { get; init; }

    /// <summary>SKU 编码</summary>
    public string SkuCode { get; init; } = string.Empty;

    /// <summary>规格</summary>
    public string Spec { get; init; } = string.Empty;

    /// <summary>秒杀价</summary>
    public decimal SeckillPrice { get; init; }

    /// <summary>秒杀总库存</summary>
    public int TotalStock { get; init; }

    /// <summary>每人限购数量</summary>
    public int LimitPerUser { get; init; }

    /// <summary>开始时间</summary>
    public DateTime StartTime { get; init; }

    /// <summary>结束时间</summary>
    public DateTime EndTime { get; init; }

    /// <summary>状态（Draft/Active/Ended）</summary>
    public SeckillStatus Status { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>秒杀记录响应（买家/商户查询）</summary>
public sealed record SeckillRecordResponse
{
    /// <summary>秒杀记录 ID</summary>
    public Guid Id { get; init; }

    /// <summary>秒杀活动 ID</summary>
    public Guid ActivityId { get; init; }

    /// <summary>商户 ID</summary>
    public Guid MerchantId { get; init; }

    /// <summary>商户名称（快照）</summary>
    public string MerchantName { get; init; } = string.Empty;

    /// <summary>买家用户 ID</summary>
    public Guid UserId { get; init; }

    /// <summary>商品名称</summary>
    public string ProductName { get; init; } = string.Empty;

    /// <summary>SKU 编码</summary>
    public string SkuCode { get; init; } = string.Empty;

    /// <summary>规格</summary>
    public string Spec { get; init; } = string.Empty;

    /// <summary>秒杀价</summary>
    public decimal UnitPrice { get; init; }

    /// <summary>购买数量</summary>
    public int Quantity { get; init; }

    /// <summary>订单支付截止</summary>
    public DateTime ExpireAt { get; init; }

    /// <summary>关联订单 ID（未下单为 null）</summary>
    public Guid? OrderId { get; init; }

    /// <summary>关联订单号（未下单为 null）</summary>
    public string? OrderNo { get; init; }

    /// <summary>状态（Pending/Ordered/Expired）</summary>
    public SeckillRecordStatus Status { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>秒杀抢购结果（买家端）</summary>
public sealed record BuySeckillResult
{
    /// <summary>是否抢购成功（库存预扣成功，等待异步下单）</summary>
    public bool Success { get; init; }

    /// <summary>失败原因（成功为 null）</summary>
    public string? Error { get; init; }

    /// <summary>秒杀记录 ID（成功时返回）</summary>
    public Guid? RecordId { get; init; }

    /// <summary>秒杀活动 ID（成功时返回）</summary>
    public Guid? ActivityId { get; init; }

    /// <summary>秒杀价（成功时返回）</summary>
    public decimal UnitPrice { get; init; }

    /// <summary>购买数量（成功时返回）</summary>
    public int Quantity { get; init; }

    /// <summary>订单支付截止（成功时返回）</summary>
    public DateTime? ExpireAt { get; init; }
}

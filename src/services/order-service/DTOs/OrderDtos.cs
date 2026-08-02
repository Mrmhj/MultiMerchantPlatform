using System.ComponentModel.DataAnnotations;
using OrderService.Domain.Enums;

namespace OrderService.DTOs;

/// <summary>订单商品项请求</summary>
public sealed record OrderItemRequest
{
    /// <summary>商户 ID（拆单维度）</summary>
    [Required]
    public Guid MerchantId { get; init; }

    /// <summary>商户名称（快照）</summary>
    [Required, StringLength(100)]
    public required string MerchantName { get; init; }

    /// <summary>商品 ID</summary>
    [Required]
    public Guid ProductId { get; init; }

    /// <summary>商品名称（快照）</summary>
    [Required, StringLength(200)]
    public required string ProductName { get; init; }

    /// <summary>SKU ID</summary>
    [Required]
    public Guid SkuId { get; init; }

    /// <summary>SKU 编码（快照）</summary>
    [Required, StringLength(50)]
    public required string SkuCode { get; init; }

    /// <summary>规格（快照，如 500g）</summary>
    [Required, StringLength(100)]
    public required string Spec { get; init; }

    /// <summary>单价（快照，元）</summary>
    [Range(0, 99999999)]
    public decimal UnitPrice { get; init; }

    /// <summary>数量</summary>
    [Range(1, 9999)]
    public int Quantity { get; init; }
}

/// <summary>创建订单请求</summary>
public sealed record CreateOrderRequest
{
    /// <summary>商品项列表（可跨商户，自动拆单）</summary>
    [Required, MinLength(1)]
    public List<OrderItemRequest> Items { get; init; } = [];

    /// <summary>买家备注（可选）</summary>
    [StringLength(500)]
    public string? Remark { get; init; }
}

/// <summary>订单商品项响应</summary>
public sealed record OrderItemResponse
{
    /// <summary>商品项 ID</summary>
    public Guid Id { get; init; }

    /// <summary>商户 ID</summary>
    public Guid MerchantId { get; init; }

    /// <summary>商品名称</summary>
    public required string ProductName { get; init; }

    /// <summary>SKU 编码</summary>
    public required string SkuCode { get; init; }

    /// <summary>规格</summary>
    public required string Spec { get; init; }

    /// <summary>单价（元）</summary>
    public decimal UnitPrice { get; init; }

    /// <summary>数量</summary>
    public int Quantity { get; init; }

    /// <summary>小计金额（元）</summary>
    public decimal Subtotal { get; init; }
}

/// <summary>子订单响应</summary>
public sealed record SubOrderResponse
{
    /// <summary>子订单 ID</summary>
    public Guid Id { get; init; }

    /// <summary>所属主订单 ID</summary>
    public Guid OrderId { get; init; }

    /// <summary>商户 ID</summary>
    public Guid MerchantId { get; init; }

    /// <summary>商户名称</summary>
    public required string MerchantName { get; init; }

    /// <summary>子单金额（元）</summary>
    public decimal TotalAmount { get; init; }

    /// <summary>子单状态（1待付款 2已付款 3已发货 4已完成 5已取消）</summary>
    public SubOrderStatus Status { get; init; }

    /// <summary>物流公司编码（已发货后非空）</summary>
    public string? CarrierCode { get; init; }

    /// <summary>物流运单号（已发货后非空）</summary>
    public string? TrackingNo { get; init; }

    /// <summary>商品项列表</summary>
    public required List<OrderItemResponse> Items { get; init; }
}

/// <summary>子订单发货请求（商户端）</summary>
public sealed record ShipSubOrderRequest
{
    /// <summary>物流公司编码（2-50 字符）</summary>
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(50, MinimumLength = 2)]
    public required string CarrierCode { get; init; }

    /// <summary>物流运单号（6-64 字符）</summary>
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(64, MinimumLength = 6)]
    public required string TrackingNo { get; init; }
}

/// <summary>已完成子订单 DTO（内部接口，供 settlement-service 生成结算单）</summary>
public sealed record CompletedSubOrderDto(
    Guid SubOrderId,
    Guid OrderId,
    string OrderNo,
    Guid MerchantId,
    string MerchantName,
    decimal TotalAmount,
    DateTime CompletedAt);

/// <summary>订单响应</summary>
public sealed record OrderResponse
{
    /// <summary>订单 ID</summary>
    public Guid Id { get; init; }

    /// <summary>业务订单号</summary>
    public required string OrderNo { get; init; }

    /// <summary>买家用户 ID</summary>
    public Guid BuyerUserId { get; init; }

    /// <summary>订单总金额（元）</summary>
    public decimal TotalAmount { get; init; }

    /// <summary>订单状态（1待付款 2已付款 3已完成 4已取消）</summary>
    public OrderStatus Status { get; init; }

    /// <summary>买家备注</summary>
    public string? Remark { get; init; }

    /// <summary>子订单列表（拆单结果）</summary>
    public required List<SubOrderResponse> SubOrders { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

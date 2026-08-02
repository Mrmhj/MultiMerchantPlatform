using System.ComponentModel.DataAnnotations;
using LogisticsService.Domain.Enums;

namespace LogisticsService.DTOs;

/// <summary>创建运单内部请求（order-service 发货回调，X-Internal-Key）</summary>
public sealed record CreateShipmentInternalRequest
{
    /// <summary>买家用户 ID</summary>
    [Required]
    public Guid BuyerUserId { get; init; }

    /// <summary>商户 ID</summary>
    [Required]
    public Guid MerchantId { get; init; }

    /// <summary>子订单 ID（唯一）</summary>
    [Required]
    public Guid SubOrderId { get; init; }

    /// <summary>主订单 ID</summary>
    [Required]
    public Guid OrderId { get; init; }

    /// <summary>订单号</summary>
    [Required, StringLength(40)]
    public required string OrderNo { get; init; }

    /// <summary>物流公司编码（物流服务按编码带出公司名称快照）</summary>
    [Required, StringLength(50)]
    public required string CarrierCode { get; init; }

    /// <summary>运单号</summary>
    [Required, StringLength(64)]
    public required string TrackingNo { get; init; }
}

/// <summary>轨迹推进内部请求（模拟物流公司回调，X-Internal-Key）</summary>
public sealed record AdvanceTrackInternalRequest
{
    /// <summary>运单号</summary>
    [Required, StringLength(64)]
    public required string TrackingNo { get; init; }

    /// <summary>轨迹描述（可选，缺省按状态默认文案）</summary>
    [StringLength(200)]
    public string? Description { get; init; }

    /// <summary>地点（可选）</summary>
    [StringLength(100)]
    public string? Location { get; init; }

    /// <summary>是否标记异常</summary>
    public bool MarkException { get; init; }
}

/// <summary>物流公司创建/更新请求（平台端）</summary>
public sealed record SaveCompanyRequest
{
    /// <summary>编码（创建时必填，2-20 字符）</summary>
    [StringLength(20, MinimumLength = 2)]
    public string? Code { get; init; }

    /// <summary>名称（1-50 字符）</summary>
    [Required, StringLength(50, MinimumLength = 1)]
    public required string Name { get; init; }

    /// <summary>查询链接模板（{no} 替换为运单号，可选）</summary>
    [StringLength(300)]
    public string? TrackingUrlTemplate { get; init; }
}

/// <summary>物流公司状态变更请求（平台端）</summary>
public sealed record ToggleCompanyRequest
{
    /// <summary>启用 true / 停用 false</summary>
    [Required]
    public bool Enabled { get; init; }
}

/// <summary>物流公司响应</summary>
public sealed record CompanyResponse
{
    /// <summary>公司 ID</summary>
    public Guid Id { get; init; }

    /// <summary>编码</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>查询链接模板</summary>
    public string? TrackingUrlTemplate { get; init; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; init; }
}

/// <summary>轨迹响应</summary>
public sealed record TrackResponse
{
    /// <summary>状态</summary>
    public ShipmentStatus Status { get; init; }

    /// <summary>轨迹描述</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>地点</summary>
    public string? Location { get; init; }

    /// <summary>轨迹时间（UTC）</summary>
    public DateTime TrackedAt { get; init; }
}

/// <summary>运单响应（详情含轨迹，列表不含轨迹）</summary>
public sealed record ShipmentResponse
{
    /// <summary>运单 ID</summary>
    public Guid Id { get; init; }

    /// <summary>商户 ID</summary>
    public Guid MerchantId { get; init; }

    /// <summary>子订单 ID</summary>
    public Guid SubOrderId { get; init; }

    /// <summary>订单号</summary>
    public string OrderNo { get; init; } = string.Empty;

    /// <summary>物流公司编码</summary>
    public string CarrierCode { get; init; } = string.Empty;

    /// <summary>物流公司名称</summary>
    public string CarrierName { get; init; } = string.Empty;

    /// <summary>运单号</summary>
    public string TrackingNo { get; init; } = string.Empty;

    /// <summary>状态</summary>
    public ShipmentStatus Status { get; init; }

    /// <summary>签收时间（未签收为 null）</summary>
    public DateTime? SignedAt { get; init; }

    /// <summary>轨迹列表（详情接口含，列表接口为空）</summary>
    public List<TrackResponse> Tracks { get; init; } = [];

    /// <summary>创建时间（UTC）</summary>
    public DateTime CreatedAt { get; init; }
}

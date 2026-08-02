using System.ComponentModel.DataAnnotations;
using MessagingService.Domain.Enums;

namespace MessagingService.DTOs;

/// <summary>发布消息请求</summary>
public sealed record PublishMessageRequest
{
    /// <summary>事件名称（如 order.created）</summary>
    [Required, StringLength(200, MinimumLength = 1)]
    public required string EventName { get; init; }

    /// <summary>消息体（JSON）</summary>
    [Required]
    public required string Payload { get; init; }

    /// <summary>路由键（可选）</summary>
    [StringLength(200)]
    public string? RoutingKey { get; init; }

    /// <summary>业务消息 ID（可选，缺省自动生成；传值用于幂等控制）</summary>
    public Guid? MessageId { get; init; }

    /// <summary>覆盖默认最大重试次数（可选）</summary>
    [Range(1, 20)]
    public int? MaxRetryCount { get; init; }
}

/// <summary>消息状态响应</summary>
public sealed record MessageResponse
{
    /// <summary>记录 ID（发件箱主键）</summary>
    public Guid Id { get; init; }

    /// <summary>业务消息 ID（全局唯一，幂等键）</summary>
    public Guid MessageId { get; init; }

    /// <summary>事件名称</summary>
    public required string EventName { get; init; }

    /// <summary>消息体（JSON）</summary>
    public required string Payload { get; init; }

    /// <summary>路由键</summary>
    public string? RoutingKey { get; init; }

    /// <summary>当前状态（Pending/Failed/Published/DeadLetter）</summary>
    public MessageStatus Status { get; init; }

    /// <summary>已重试次数</summary>
    public int RetryCount { get; init; }

    /// <summary>最大重试次数</summary>
    public int MaxRetryCount { get; init; }

    /// <summary>下次可投递时间</summary>
    public DateTime? NextRetryTime { get; init; }

    /// <summary>成功投递时间</summary>
    public DateTime? PublishedAt { get; init; }

    /// <summary>最近一次错误信息</summary>
    public string? LastError { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>注册订阅请求</summary>
public sealed record RegisterSubscriptionRequest
{
    /// <summary>订阅的事件名（支持 * 通配订阅全部事件）</summary>
    [Required, StringLength(200, MinimumLength = 1)]
    public required string EventName { get; init; }

    /// <summary>回调地址（POST 接收消息）</summary>
    [Required, StringLength(500, MinimumLength = 1)]
    public required string CallbackUrl { get; init; }

    /// <summary>订阅者服务名（便于识别）</summary>
    [StringLength(100)]
    public string? ServiceName { get; init; }

    /// <summary>覆盖默认最大重试次数（可选）</summary>
    [Range(1, 20)]
    public int? MaxRetryCount { get; init; }
}

/// <summary>订阅响应</summary>
public sealed record SubscriptionResponse
{
    /// <summary>订阅 ID</summary>
    public Guid Id { get; init; }

    /// <summary>订阅的事件名（支持 * 通配）</summary>
    public required string EventName { get; init; }

    /// <summary>回调地址</summary>
    public required string CallbackUrl { get; init; }

    /// <summary>订阅者服务名</summary>
    public string? ServiceName { get; init; }

    /// <summary>覆盖的最大重试次数</summary>
    public int? MaxRetryCount { get; init; }

    /// <summary>是否启用</summary>
    public bool IsActive { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

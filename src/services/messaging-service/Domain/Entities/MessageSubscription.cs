using BuildingBlocks.Core.Entities;

namespace MessagingService.Domain.Entities;

/// <summary>
/// 消息订阅 — 订阅者回调注册。
/// 一个事件可被多个订阅者订阅，分发器向每个订阅者的回调地址投递消息。
/// </summary>
public sealed class MessageSubscription : Entity
{
    private MessageSubscription() { } // EF Core

    public MessageSubscription(
        string eventName,
        string callbackUrl,
        string? serviceName = null,
        int? maxRetryCount = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(callbackUrl);

        EventName = eventName;
        CallbackUrl = callbackUrl;
        ServiceName = serviceName;
        MaxRetryCount = maxRetryCount;
        IsActive = true;
    }

    /// <summary>订阅的事件名，如 order.created</summary>
    public string EventName { get; private set; } = null!;

    /// <summary>回调地址（订阅者的接收端点，POST）</summary>
    public string CallbackUrl { get; private set; } = null!;

    /// <summary>订阅者服务名（便于识别）</summary>
    public string? ServiceName { get; private set; }

    /// <summary>覆盖全局默认的最大重试次数（可选）</summary>
    public int? MaxRetryCount { get; private set; }

    /// <summary>是否启用</summary>
    public bool IsActive { get; private set; }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}

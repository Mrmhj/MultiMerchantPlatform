using BuildingBlocks.Core.Entities;
using MessagingService.Domain.Enums;

namespace MessagingService.Domain.Entities;

/// <summary>
/// 消息发件箱 — 持久化的待发送消息。
/// 采用 Outbox 模式：消息先落库，再由后台分发器投递，保证不丢消息。
/// </summary>
public sealed class MessageOutbox : Entity
{
    private MessageOutbox() { } // EF Core

    /// <summary>创建待发送消息（初始状态 Pending）</summary>
    /// <param name="messageId">业务消息 ID（全局唯一）</param>
    /// <param name="eventName">事件名称，如 order.created</param>
    /// <param name="payload">消息体（JSON）</param>
    /// <param name="routingKey">路由键（可选）</param>
    /// <param name="maxRetryCount">最大重试次数（默认 5）</param>
    public MessageOutbox(
        Guid messageId,
        string eventName,
        string payload,
        string? routingKey = null,
        int maxRetryCount = 5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        MessageId = messageId;
        EventName = eventName;
        Payload = payload;
        RoutingKey = routingKey;
        MaxRetryCount = maxRetryCount;
        Status = MessageStatus.Pending;
        RetryCount = 0;
        NextRetryTime = DateTime.UtcNow;
    }

    /// <summary>业务消息 ID（全局唯一，幂等键）</summary>
    public Guid MessageId { get; private set; }

    /// <summary>事件名称，如 order.created</summary>
    public string EventName { get; private set; } = null!;

    /// <summary>消息体（JSON）</summary>
    public string Payload { get; private set; } = null!;

    /// <summary>路由键（可选，用于精细匹配订阅者）</summary>
    public string? RoutingKey { get; private set; }

    /// <summary>当前状态</summary>
    public MessageStatus Status { get; private set; }

    /// <summary>已重试次数</summary>
    public int RetryCount { get; private set; }

    /// <summary>最大重试次数（超过转死信）</summary>
    public int MaxRetryCount { get; private set; }

    /// <summary>下次可投递时间（指数退避）</summary>
    public DateTime? NextRetryTime { get; private set; }

    /// <summary>成功投递时间</summary>
    public DateTime? PublishedAt { get; private set; }

    /// <summary>最近一次错误信息</summary>
    public string? LastError { get; private set; }

    /// <summary>是否到投递时间（Pending 或 Failed 且到点）</summary>
    public bool IsDue(TimeProvider timeProvider) =>
        Status is MessageStatus.Pending or MessageStatus.Failed
        && NextRetryTime <= timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>
    /// 标记一次投递失败 — 重试次数 +1，按指数退避计算下次投递时间；超过上限转死信。
    /// </summary>
    public void MarkFailed(string error, TimeProvider timeProvider, TimeSpan baseInterval, int maxDelaySeconds = 300)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        RetryCount++;
        LastError = error;

        if (RetryCount >= MaxRetryCount)
        {
            Status = MessageStatus.DeadLetter;
            NextRetryTime = null;
        }
        else
        {
            Status = MessageStatus.Failed;
            var delaySeconds = Math.Min(maxDelaySeconds, baseInterval.TotalSeconds * Math.Pow(2, RetryCount - 1));
            NextRetryTime = timeProvider.GetUtcNow().UtcDateTime.AddSeconds(delaySeconds);
        }
    }

    /// <summary>标记为已发布</summary>
    public void MarkPublished(TimeProvider timeProvider)
    {
        Status = MessageStatus.Published;
        PublishedAt = timeProvider.GetUtcNow().UtcDateTime;
        NextRetryTime = null;
        LastError = null;
    }

    /// <summary>重置为重试状态（管理端手动重发死信消息）</summary>
    public void ResetForRetry(TimeProvider timeProvider)
    {
        Status = MessageStatus.Pending;
        RetryCount = 0;
        NextRetryTime = timeProvider.GetUtcNow().UtcDateTime;
        LastError = null;
    }

    /// <summary>标记为死信（管理端手动）</summary>
    public void MoveToDeadLetter(string? reason = null)
    {
        Status = MessageStatus.DeadLetter;
        NextRetryTime = null;
        LastError = reason ?? LastError;
    }
}

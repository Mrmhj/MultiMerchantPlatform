using BuildingBlocks.Core.Entities;

namespace MessagingService.Domain.Entities;

/// <summary>
/// 消息幂等记录 — 记录每个订阅者已成功消费的消息。
/// 防止网络超时等场景下重复投递导致业务重复处理（至少一次投递 + 幂等去重）。
/// </summary>
public sealed class MessageIdempotency : Entity
{
    private MessageIdempotency() { } // EF Core

    public MessageIdempotency(Guid messageId, string consumerUrl)
    {
        MessageId = messageId;
        ConsumerUrl = consumerUrl;
        ConsumedAt = DateTime.UtcNow;
    }

    /// <summary>消息 ID</summary>
    public Guid MessageId { get; private set; }

    /// <summary>消费者回调地址</summary>
    public string ConsumerUrl { get; private set; } = null!;

    /// <summary>消费时间</summary>
    public DateTime ConsumedAt { get; private set; }
}

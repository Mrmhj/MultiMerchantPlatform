using System.Text.Json;
using BuildingBlocks.Core.Events;

namespace BuildingBlocks.Messaging;

/// <summary>
/// 消息发布者接口 — 各服务通过此接口发布集成事件。
/// </summary>
public interface IMessagePublisher
{
    Task PublishAsync<T>(T message, string? routingKey = null, CancellationToken ct = default)
        where T : IIntegrationEvent;
}

/// <summary>
/// 消息消费者接口 — 各服务实现此接口消费消息。
/// </summary>
public interface IMessageConsumer<in T> where T : IIntegrationEvent
{
    Task HandleAsync(T message, CancellationToken ct = default);
}

/// <summary>
/// 消息信封 — 包装消息元数据。
/// </summary>
public record MessageEnvelope
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string EventName { get; init; }
    public required string Payload { get; init; }
    public string? RoutingKey { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public int RetryCount { get; init; }
    public DateTime? ScheduledAt { get; init; }

    public static MessageEnvelope Create<T>(T message, string? routingKey = null) where T : IIntegrationEvent
        => new()
        {
            EventName = message.EventName,
            Payload = JsonSerializer.Serialize(message, typeof(T)),
            RoutingKey = routingKey ?? message.EventName,
            CreatedAt = message.OccurredOn
        };
}

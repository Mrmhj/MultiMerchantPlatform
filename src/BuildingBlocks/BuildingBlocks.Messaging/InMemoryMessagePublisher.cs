using System.Collections.Concurrent;

namespace BuildingBlocks.Messaging;

/// <summary>
/// In-Memory 消息发布者 — 开发环境使用。
/// 生产环境替换为 messaging-service 的 HTTP API 客户端。
/// </summary>
public class InMemoryMessagePublisher : IMessagePublisher
{
    private readonly ConcurrentQueue<MessageEnvelope> _queue = new();

    public Task PublishAsync<T>(T message, string? routingKey = null, CancellationToken ct = default)
        where T : Core.Events.IIntegrationEvent
    {
        var envelope = MessageEnvelope.Create(message, routingKey);
        _queue.Enqueue(envelope);
        return Task.CompletedTask;
    }

    public IEnumerable<MessageEnvelope> DequeueAll()
    {
        while (_queue.TryDequeue(out var envelope))
            yield return envelope;
    }
}

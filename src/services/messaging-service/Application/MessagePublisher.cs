using BuildingBlocks.Core.Events;
using BuildingBlocks.Messaging;
using MessagingService.Application.Options;
using MessagingService.Domain.Entities;
using MessagingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MessagingService.Application;

/// <summary>
/// 消息发布器 — 实现 Outbox 模式：消息先落库（MessageOutbox），再由后台分发器异步投递。
/// 业务服务通过 <see cref="IMessagePublisher"/> 发布集成事件。
/// </summary>
public sealed class MessagePublisher(
    MessagingDbContext db,
    IOptions<MessagingOptions> options) : IMessagePublisher
{
    /// <inheritdoc />
    public async Task PublishAsync<T>(T message, string? routingKey = null, CancellationToken ct = default)
        where T : IIntegrationEvent
    {
        var envelope = MessageEnvelope.Create(message, routingKey);

        var outbox = new MessageOutbox(
            envelope.Id,
            envelope.EventName,
            envelope.Payload,
            envelope.RoutingKey,
            options.Value.DefaultMaxRetryCount);

        db.MessageOutboxes.Add(outbox);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 批量发布（同一事务内落库）。
    /// </summary>
    public async Task PublishBatchAsync<T>(IEnumerable<T> messages, CancellationToken ct = default)
        where T : IIntegrationEvent
    {
        foreach (var message in messages)
        {
            var envelope = MessageEnvelope.Create(message);
            db.MessageOutboxes.Add(new MessageOutbox(
                envelope.Id, envelope.EventName, envelope.Payload, envelope.RoutingKey,
                options.Value.DefaultMaxRetryCount));
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// 直接发布原始消息（不经过 IIntegrationEvent 类型，由 Controller 使用）。
    /// </summary>
    public async Task<Guid> PublishRawAsync(string eventName, string payload, string? routingKey = null,
        int? maxRetryCount = null, Guid? messageId = null, CancellationToken ct = default)
    {
        var id = messageId ?? Guid.NewGuid();
        db.MessageOutboxes.Add(new MessageOutbox(
            id, eventName, payload, routingKey,
            maxRetryCount ?? options.Value.DefaultMaxRetryCount));
        await db.SaveChangesAsync(ct);
        return id;
    }
}

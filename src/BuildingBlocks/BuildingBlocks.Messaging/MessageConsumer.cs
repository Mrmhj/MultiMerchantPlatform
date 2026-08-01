using System.Text.Json;
using BuildingBlocks.Core.Events;

namespace BuildingBlocks.Messaging;

/// <summary>
/// 消息消费结果。
/// </summary>
public readonly record struct ConsumeResult(bool IsSuccess, Guid MessageId, string? Error)
{
    public static ConsumeResult Success(Guid messageId) => new(true, messageId, null);

    public static ConsumeResult Failure(Guid messageId, string error) => new(false, messageId, error);
}

/// <summary>
/// 消息消费者基类 — 订阅者服务继承并实现 <see cref="HandleAsync"/> 处理业务逻辑。
/// 使用方式：在订阅者服务的 Controller 端点接收 MessageEnvelope，调用 <see cref="ConsumeAsync"/>。
/// 幂等提示：<paramref name="envelope"/> 的 Id 即 X-Message-Id，业务可用其作为幂等键（配合数据库唯一约束）。
/// </summary>
public abstract class MessageConsumer<T> where T : IIntegrationEvent
{
    /// <summary>处理业务消息（子类实现）</summary>
    protected abstract Task HandleAsync(T message, CancellationToken ct = default);

    /// <summary>消费消息信封（反序列化 + 业务处理）</summary>
    public async Task<ConsumeResult> ConsumeAsync(MessageEnvelope envelope, CancellationToken ct = default)
    {
        try
        {
            var message = JsonSerializer.Deserialize<T>(envelope.Payload);
            if (message is null)
                return ConsumeResult.Failure(envelope.Id, "消息反序列化失败");

            await HandleAsync(message, ct);
            return ConsumeResult.Success(envelope.Id);
        }
        catch (Exception ex)
        {
            return ConsumeResult.Failure(envelope.Id, ex.Message);
        }
    }
}

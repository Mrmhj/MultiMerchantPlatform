using System.Net.Http.Json;
using BuildingBlocks.Core.Events;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Messaging;

/// <summary>
/// HTTP 消息发布者 — 通过 messaging-service 的 REST API 发布消息（Strategy 模式 — HTTP 策略）。
/// 生产环境默认实现：消息持久化到 SQL Server，由 messaging-service 异步投递。
/// </summary>
public sealed class HttpMessagePublisher(
    HttpClient httpClient,
    IOptions<MessageBusOptions> options) : IMessagePublisher
{
    private readonly MessageBusOptions _options = options.Value;

    /// <inheritdoc />
    public async Task PublishAsync<T>(T message, string? routingKey = null, CancellationToken ct = default)
        where T : IIntegrationEvent
    {
        var envelope = MessageEnvelope.Create(message, routingKey);

        var request = new PublishRequest
        {
            EventName = envelope.EventName,
            Payload = envelope.Payload,
            RoutingKey = envelope.RoutingKey,
        };

        httpClient.BaseAddress ??= new Uri(_options.BaseUrl);
        httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        using var response = await httpClient.PostAsJsonAsync("/api/messages", request, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>发布请求体（与 messaging-service 的 PublishMessageRequest 契约一致）</summary>
    private sealed record PublishRequest
    {
        public required string EventName { get; init; }
        public required string Payload { get; init; }
        public string? RoutingKey { get; init; }
    }
}

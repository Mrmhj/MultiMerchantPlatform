using System.Net;
using BuildingBlocks.Messaging;
using MessagingService.Application.Options;
using MessagingService.Domain.Entities;
using MessagingService.Domain.Enums;
using MessagingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MessagingService.Application;

/// <summary>
/// 投递结果。
/// </summary>
public readonly record struct DeliveryResult(bool Success, string? Error);

/// <summary>
/// 消息分发器 — 后台服务轮询待发送消息，向订阅者回调地址投递。
/// 特性：指数退避重试 / 幂等去重（Idempotency 表）/ 超限转死信。
/// </summary>
public sealed class MessageDispatcher(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider,
    IOptions<MessagingOptions> options,
    ILogger<MessageDispatcher> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("消息分发器已启动，轮询间隔 {Interval}s，批次 {Batch} 条",
            options.Value.PollIntervalSeconds, options.Value.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "消息分发批次执行异常");
            }

            await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), stoppingToken);
        }
    }

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MessagingDbContext>();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var due = await db.MessageOutboxes
            .Where(m => (m.Status == MessageStatus.Pending || m.Status == MessageStatus.Failed)
                        && m.NextRetryTime <= now)
            .OrderBy(m => m.NextRetryTime)
            .Take(options.Value.BatchSize)
            .ToListAsync(ct);

        if (due.Count == 0)
            return;

        logger.LogInformation("发现 {Count} 条待分发消息", due.Count);

        foreach (var message in due)
        {
            await DispatchMessageAsync(db, message, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task DispatchMessageAsync(MessagingDbContext db, MessageOutbox message, CancellationToken ct)
    {
        // 匹配订阅者：EventName 精确匹配（支持 * 通配订阅）
        var subscriptions = await db.Subscriptions
            .Where(s => s.IsActive && (s.EventName == message.EventName || s.EventName == "*"))
            .ToListAsync(ct);

        if (subscriptions.Count == 0)
        {
            // 无订阅者视为已投递（与 RabbitMQ topic 无消费者语义一致）
            message.MarkPublished(timeProvider);
            return;
        }

        foreach (var sub in subscriptions)
        {
            // 幂等去重：该订阅者已成功消费过此消息则跳过
            var alreadyConsumed = await db.IdempotencyRecords
                .AnyAsync(r => r.MessageId == message.MessageId && r.ConsumerUrl == sub.CallbackUrl, ct);
            if (alreadyConsumed)
                continue;

            var result = await DeliverAsync(message, sub, ct);

            if (result.Success)
            {
                db.IdempotencyRecords.Add(new MessageIdempotency(message.MessageId, sub.CallbackUrl));
            }
            else
            {
                var effectiveMax = sub.MaxRetryCount ?? message.MaxRetryCount;
                var baseInterval = TimeSpan.FromSeconds(options.Value.RetryBaseIntervalSeconds);
                message.MarkFailed(result.Error ?? "未知错误", timeProvider, baseInterval,
                    options.Value.MaxRetryDelaySeconds);

                if (message.Status == MessageStatus.DeadLetter)
                {
                    logger.LogWarning("消息 {MessageId} 投递至 {Url} 超过 {Max} 次失败，转入死信：{Error}",
                        message.MessageId, sub.CallbackUrl, effectiveMax, result.Error);
                }

                return; // 失败即中断，等下次轮询重试
            }
        }

        message.MarkPublished(timeProvider);
    }

    private async Task<DeliveryResult> DeliverAsync(MessageOutbox message, MessageSubscription sub, CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("MessagingDispatcher");
            client.Timeout = TimeSpan.FromSeconds(options.Value.HttpClientTimeoutSeconds);

            var envelope = new MessageEnvelope
            {
                Id = message.MessageId,
                EventName = message.EventName,
                Payload = message.Payload,
                RoutingKey = message.RoutingKey,
                CreatedAt = message.CreatedAt,
                RetryCount = message.RetryCount,
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, sub.CallbackUrl)
            {
                Content = JsonContent.Create(envelope),
            };
            // 幂等头：订阅者据此去重
            request.Headers.TryAddWithoutValidation("X-Message-Id", message.MessageId.ToString());
            request.Headers.TryAddWithoutValidation("X-Event-Name", message.EventName);
            if (!string.IsNullOrWhiteSpace(message.RoutingKey))
                request.Headers.TryAddWithoutValidation("X-Routing-Key", message.RoutingKey);

            using var response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return new DeliveryResult(true, null);

            var body = await response.Content.ReadAsStringAsync(ct);
            var truncated = body.Length > 500 ? body[..500] : body;
            return new DeliveryResult(false, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {truncated}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new DeliveryResult(false, "投递被取消");
        }
        catch (HttpRequestException ex)
        {
            return new DeliveryResult(false, ex.Message);
        }
        catch (TaskCanceledException ex)
        {
            return new DeliveryResult(false, $"请求超时: {ex.Message}");
        }
    }
}

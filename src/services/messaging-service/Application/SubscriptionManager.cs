using MessagingService.Domain.Entities;
using MessagingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MessagingService.Application;

/// <summary>
/// 订阅管理器 — 订阅注册 / 取消 / 查询。
/// 相同 EventName + CallbackUrl 视为同一订阅（幂等注册）。
/// </summary>
public sealed class SubscriptionManager(MessagingDbContext db)
{
    /// <summary>注册订阅（已存在则重新激活，幂等）</summary>
    public async Task<MessageSubscription> RegisterAsync(
        string eventName, string callbackUrl, string? serviceName = null,
        int? maxRetryCount = null, CancellationToken ct = default)
    {
        var existing = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.EventName == eventName && s.CallbackUrl == callbackUrl, ct);

        if (existing is not null)
        {
            existing.Activate();
            return existing;
        }

        var subscription = new MessageSubscription(eventName, callbackUrl, serviceName, maxRetryCount);
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync(ct);
        return subscription;
    }

    /// <summary>取消订阅（软停用，保留记录）</summary>
    public async Task<bool> UnregisterAsync(Guid id, CancellationToken ct = default)
    {
        var subscription = await db.Subscriptions.FindAsync([id], ct);
        if (subscription is null)
            return false;

        subscription.Deactivate();
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>启用订阅</summary>
    public async Task<bool> ActivateAsync(Guid id, CancellationToken ct = default)
    {
        var subscription = await db.Subscriptions.FindAsync([id], ct);
        if (subscription is null)
            return false;

        subscription.Activate();
        await db.SaveChangesAsync(ct);
        return true;
    }
}

using Microsoft.Extensions.Options;

namespace NotificationService.Application.Services;

/// <summary>
/// 通知服务配置（短信/Push 渠道策略）。
/// </summary>
public sealed class NotificationOptions
{
    /// <summary>配置节名称（appsettings.json 的 Notification 节点）</summary>
    public const string SectionName = "Notification";

    /// <summary>
    /// 短信 DryRun：true 时不真实下发，仅落库标记成功（本地无短信网关时使用）。
    /// 生产环境必须设为 false 并接入真实短信网关（扩展点 INotificationChannelSender）。
    /// </summary>
    public bool SmsDryRun { get; set; } = true;

    /// <summary>
    /// Push DryRun：true 时不真实下发，仅落库标记成功（本地无推送通道时使用）。
    /// 生产环境必须设为 false 并接入真实推送通道（扩展点 INotificationChannelSender）。
    /// </summary>
    public bool PushDryRun { get; set; } = true;

    /// <summary>通知保留天数（过期站内信可归档清理，默认 180）</summary>
    public int RetentionDays { get; set; } = 180;
}

/// <summary>
/// 短信发送器 — DryRun 模式下仅落库标记成功；真实模式预留第三方网关扩展点。
/// </summary>
public sealed class SmsSender(IOptions<NotificationOptions> options, TimeProvider timeProvider)
{
    private readonly NotificationOptions _options = options.Value;

    /// <summary>是否 DryRun 模式</summary>
    public bool IsDryRun => _options.SmsDryRun;

    /// <summary>发送短信（DryRun 直接成功）</summary>
    /// <param name="sms">短信实体</param>
    /// <param name="ct">取消令牌</param>
    public Task SendAsync(NotificationService.Domain.Entities.SmsMessage sms, CancellationToken ct = default)
    {
        // 扩展点：生产环境在此接入阿里云/腾讯云短信网关，失败抛异常由调用方标记 Failed
        sms.MarkSent(timeProvider.GetUtcNow().UtcDateTime);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Push 推送器 — DryRun 模式下仅落库标记成功；真实模式预留第三方推送扩展点。
/// </summary>
public sealed class PushSender(IOptions<NotificationOptions> options, TimeProvider timeProvider)
{
    private readonly NotificationOptions _options = options.Value;

    /// <summary>是否 DryRun 模式</summary>
    public bool IsDryRun => _options.PushDryRun;

    /// <summary>发送 Push（DryRun 直接成功）</summary>
    /// <param name="push">推送实体</param>
    /// <param name="ct">取消令牌</param>
    public Task SendAsync(NotificationService.Domain.Entities.PushMessage push, CancellationToken ct = default)
    {
        // 扩展点：生产环境在此接入极光/个推/APNs/FCM，失败抛异常由调用方标记 Failed
        push.MarkSent(timeProvider.GetUtcNow().UtcDateTime);
        return Task.CompletedTask;
    }
}

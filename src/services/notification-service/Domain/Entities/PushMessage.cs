using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;
using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

/// <summary>
/// App Push 推送记录 — 独立渠道记录（开发环境 DryRun 模拟，不真实下发）。
/// </summary>
public sealed class PushMessage : Entity, IAggregateRoot
{
    private PushMessage() { } // EF Core

    /// <summary>创建推送</summary>
    /// <param name="deviceToken">设备令牌</param>
    /// <param name="title">推送标题</param>
    /// <param name="content">推送内容</param>
    /// <param name="maxRetryCount">最大重试次数（默认 3）</param>
    public PushMessage(string deviceToken, string title, string content, int maxRetryCount = 3)
    {
        DeviceToken = ValidateDeviceToken(deviceToken);
        Title = ValidateTitle(title);
        Content = ValidateContent(content);
        Status = PushStatus.Pending;
        RetryCount = 0;
        MaxRetryCount = Math.Clamp(maxRetryCount, 0, 10);
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>设备令牌</summary>
    public string DeviceToken { get; private set; } = string.Empty;

    /// <summary>推送标题</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>推送内容</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>推送状态</summary>
    public PushStatus Status { get; private set; }

    /// <summary>已重试次数</summary>
    public int RetryCount { get; private set; }

    /// <summary>最大重试次数</summary>
    public int MaxRetryCount { get; private set; }

    /// <summary>最近错误信息</summary>
    public string? LastError { get; private set; }

    /// <summary>推送时间</summary>
    public DateTime? SentAt { get; private set; }

    /// <summary>标记推送成功</summary>
    /// <param name="sentAt">推送时间</param>
    public void MarkSent(DateTime sentAt)
    {
        Status = PushStatus.Sent;
        SentAt = sentAt;
        LastError = null;
        UpdatedAt = sentAt;
    }

    /// <summary>标记失败（超出最大重试次数转死信）</summary>
    /// <param name="error">错误信息</param>
    /// <param name="failedAt">失败时间</param>
    public void MarkFailed(string error, DateTime failedAt)
    {
        Status = RetryCount + 1 >= MaxRetryCount ? PushStatus.DeadLetter : PushStatus.Failed;
        LastError = error;
        UpdatedAt = failedAt;
    }

    /// <summary>重置待推送（手动重试）</summary>
    public void ResetForRetry()
    {
        Status = PushStatus.Pending;
        LastError = null;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string ValidateDeviceToken(string deviceToken)
    {
        var trimmed = (deviceToken ?? string.Empty).Trim();
        if (trimmed.Length is < 8 or > 256)
            throw new DomainException("设备令牌长度需在 8-256 字符之间", "INVALID_DEVICE_TOKEN");
        return trimmed;
    }

    private static string ValidateTitle(string title)
    {
        var trimmed = (title ?? string.Empty).Trim();
        if (trimmed.Length is < 1 or > 200)
            throw new DomainException("推送标题长度需在 1-200 字符之间", "INVALID_PUSH_TITLE");
        return trimmed;
    }

    private static string ValidateContent(string content)
    {
        var trimmed = (content ?? string.Empty).Trim();
        if (trimmed.Length is < 1 or > 1000)
            throw new DomainException("推送内容长度需在 1-1000 字符之间", "INVALID_PUSH_CONTENT");
        return trimmed;
    }
}

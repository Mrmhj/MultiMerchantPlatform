using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;
using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

/// <summary>
/// 短信发送记录 — 独立渠道记录（开发环境 DryRun 模拟，不真实下发）。
/// </summary>
public sealed class SmsMessage : Entity, IAggregateRoot
{
    private SmsMessage() { } // EF Core

    /// <summary>创建短信</summary>
    /// <param name="phone">接收手机号</param>
    /// <param name="content">短信内容</param>
    /// <param name="maxRetryCount">最大重试次数（默认 3）</param>
    public SmsMessage(string phone, string content, int maxRetryCount = 3)
    {
        Phone = ValidatePhone(phone);
        Content = ValidateContent(content);
        Status = SmsStatus.Pending;
        RetryCount = 0;
        MaxRetryCount = Math.Clamp(maxRetryCount, 0, 10);
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>接收手机号</summary>
    public string Phone { get; private set; } = string.Empty;

    /// <summary>短信内容</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>发送状态</summary>
    public SmsStatus Status { get; private set; }

    /// <summary>已重试次数</summary>
    public int RetryCount { get; private set; }

    /// <summary>最大重试次数</summary>
    public int MaxRetryCount { get; private set; }

    /// <summary>最近错误信息</summary>
    public string? LastError { get; private set; }

    /// <summary>发送时间</summary>
    public DateTime? SentAt { get; private set; }

    /// <summary>标记发送成功</summary>
    /// <param name="sentAt">发送时间</param>
    public void MarkSent(DateTime sentAt)
    {
        Status = SmsStatus.Sent;
        SentAt = sentAt;
        LastError = null;
        UpdatedAt = sentAt;
    }

    /// <summary>标记失败（超出最大重试次数转死信）</summary>
    /// <param name="error">错误信息</param>
    /// <param name="failedAt">失败时间</param>
    public void MarkFailed(string error, DateTime failedAt)
    {
        Status = RetryCount + 1 >= MaxRetryCount ? SmsStatus.DeadLetter : SmsStatus.Failed;
        LastError = error;
        UpdatedAt = failedAt;
    }

    /// <summary>重置待发送（手动重试）</summary>
    public void ResetForRetry()
    {
        Status = SmsStatus.Pending;
        LastError = null;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string ValidatePhone(string phone)
    {
        var trimmed = (phone ?? string.Empty).Trim();
        if (trimmed.Length is < 5 or > 20)
            throw new DomainException("手机号格式不正确", "INVALID_PHONE");
        return trimmed;
    }

    private static string ValidateContent(string content)
    {
        var trimmed = (content ?? string.Empty).Trim();
        if (trimmed.Length is < 1 or > 500)
            throw new DomainException("短信内容长度需在 1-500 字符之间", "INVALID_SMS_CONTENT");
        return trimmed;
    }
}

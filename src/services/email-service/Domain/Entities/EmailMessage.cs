using BuildingBlocks.Core.Entities;
using EmailService.Domain.Enums;

namespace EmailService.Domain.Entities;

/// <summary>
/// 邮件消息 — 待发送/已发送的邮件记录。
/// </summary>
public sealed class EmailMessage : Entity
{
    private EmailMessage() { } // EF Core

    public EmailMessage(
        string from,
        string to,
        string subject,
        string body,
        bool isHtml = true,
        string? cc = null,
        string? bcc = null,
        string? templateName = null,
        int maxRetryCount = 3)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);

        From = from;
        To = to;
        Cc = cc;
        Bcc = bcc;
        Subject = subject;
        Body = body;
        IsHtml = isHtml;
        TemplateName = templateName;
        MaxRetryCount = maxRetryCount;
        Status = EmailStatus.Pending;
        RetryCount = 0;
        NextRetryTime = DateTime.UtcNow;
    }

    /// <summary>发件人</summary>
    public string From { get; private set; } = null!;

    /// <summary>收件人（多个用 ; 分隔）</summary>
    public string To { get; private set; } = null!;

    /// <summary>抄送（; 分隔）</summary>
    public string? Cc { get; private set; }

    /// <summary>密送（; 分隔）</summary>
    public string? Bcc { get; private set; }

    /// <summary>主题</summary>
    public string Subject { get; private set; } = null!;

    /// <summary>正文</summary>
    public string Body { get; private set; } = null!;

    /// <summary>是否 HTML 正文</summary>
    public bool IsHtml { get; private set; }

    /// <summary>使用的模板名（可选）</summary>
    public string? TemplateName { get; private set; }

    /// <summary>状态</summary>
    public EmailStatus Status { get; private set; }

    /// <summary>已重试次数</summary>
    public int RetryCount { get; private set; }

    /// <summary>最大重试次数</summary>
    public int MaxRetryCount { get; private set; }

    /// <summary>下次重试时间（指数退避）</summary>
    public DateTime? NextRetryTime { get; private set; }

    /// <summary>发送成功时间</summary>
    public DateTime? SentAt { get; private set; }

    /// <summary>最近一次错误</summary>
    public string? LastError { get; private set; }

    /// <summary>是否到重试时间</summary>
    public bool IsDue(TimeProvider timeProvider) =>
        Status is EmailStatus.Pending or EmailStatus.Failed
        && NextRetryTime <= timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>标记发送成功</summary>
    public void MarkSent(TimeProvider timeProvider)
    {
        Status = EmailStatus.Sent;
        SentAt = timeProvider.GetUtcNow().UtcDateTime;
        NextRetryTime = null;
        LastError = null;
    }

    /// <summary>标记发送失败（指数退避，超限转死信）</summary>
    public void MarkFailed(string error, TimeProvider timeProvider, TimeSpan baseInterval, int maxDelaySeconds = 600)
    {
        RetryCount++;
        LastError = error;

        if (RetryCount >= MaxRetryCount)
        {
            Status = EmailStatus.DeadLetter;
            NextRetryTime = null;
        }
        else
        {
            Status = EmailStatus.Failed;
            var delaySeconds = Math.Min(maxDelaySeconds, baseInterval.TotalSeconds * Math.Pow(2, RetryCount - 1));
            NextRetryTime = timeProvider.GetUtcNow().UtcDateTime.AddSeconds(delaySeconds);
        }
    }

    /// <summary>重置重试（管理端手动重发死信）</summary>
    public void ResetForRetry(TimeProvider timeProvider)
    {
        Status = EmailStatus.Pending;
        RetryCount = 0;
        NextRetryTime = timeProvider.GetUtcNow().UtcDateTime;
        LastError = null;
    }

    /// <summary>手动转死信</summary>
    public void MoveToDeadLetter(string? reason = null)
    {
        Status = EmailStatus.DeadLetter;
        NextRetryTime = null;
        LastError = reason ?? LastError;
    }
}

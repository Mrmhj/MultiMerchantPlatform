namespace EmailService.Domain.Enums;

/// <summary>邮件发送状态</summary>
public enum EmailStatus
{
    /// <summary>待发送</summary>
    Pending = 0,

    /// <summary>发送成功</summary>
    Sent = 1,

    /// <summary>发送失败（重试中）</summary>
    Failed = 2,

    /// <summary>死信（超过最大重试次数）</summary>
    DeadLetter = 3,
}

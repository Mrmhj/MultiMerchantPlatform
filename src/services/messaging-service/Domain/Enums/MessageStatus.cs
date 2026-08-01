namespace MessagingService.Domain.Enums;

/// <summary>
/// 消息状态
/// </summary>
public enum MessageStatus
{
    /// <summary>待发送</summary>
    Pending = 0,

    /// <summary>已成功投递给所有订阅者</summary>
    Published = 1,

    /// <summary>发送失败（重试中）</summary>
    Failed = 2,

    /// <summary>死信（超过最大重试次数）</summary>
    DeadLetter = 3,
}

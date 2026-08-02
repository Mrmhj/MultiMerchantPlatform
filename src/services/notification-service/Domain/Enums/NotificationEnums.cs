namespace NotificationService.Domain.Enums;

/// <summary>
/// 通知渠道 — 一条通知可同时走多个渠道（位标志）。
/// </summary>
[Flags]
public enum NotificationChannel
{
    /// <summary>站内信（通知中心收件箱）</summary>
    InApp = 1,

    /// <summary>短信（SMS，开发环境 DryRun 模拟）</summary>
    Sms = 2,

    /// <summary>App Push 推送（开发环境 DryRun 模拟）</summary>
    Push = 4,
}

/// <summary>
/// 通知业务类型 — 用于前端分类展示与筛选。
/// </summary>
public enum NotificationType
{
    /// <summary>订单</summary>
    Order = 1,

    /// <summary>支付</summary>
    Payment = 2,

    /// <summary>物流</summary>
    Logistics = 3,

    /// <summary>营销/优惠</summary>
    Promotion = 4,

    /// <summary>系统/平台</summary>
    System = 5,

    /// <summary>风控告警</summary>
    Risk = 6,

    /// <summary>监控告警（性能/日志）</summary>
    Monitor = 7,
}

/// <summary>
/// 短信发送状态。
/// </summary>
public enum SmsStatus
{
    /// <summary>待发送</summary>
    Pending = 0,

    /// <summary>发送成功</summary>
    Sent = 1,

    /// <summary>发送失败（等待重试）</summary>
    Failed = 2,

    /// <summary>超出重试次数，转入死信</summary>
    DeadLetter = 3,
}

/// <summary>
/// App Push 推送状态。
/// </summary>
public enum PushStatus
{
    /// <summary>待推送</summary>
    Pending = 0,

    /// <summary>推送成功</summary>
    Sent = 1,

    /// <summary>推送失败（等待重试）</summary>
    Failed = 2,

    /// <summary>超出重试次数，转入死信</summary>
    DeadLetter = 3,
}

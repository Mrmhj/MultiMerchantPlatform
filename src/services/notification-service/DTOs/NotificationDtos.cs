using NotificationService.Domain.Enums;

namespace NotificationService.DTOs;

/// <summary>发送站内信请求（内部接口）</summary>
public sealed record SendInAppNotificationRequest
{
    /// <summary>接收用户 ID（必填）</summary>
    public Guid UserId { get; init; }

    /// <summary>业务归属商户 ID（平台级通知可空）</summary>
    public Guid? MerchantId { get; init; }

    /// <summary>通知业务类型（默认系统通知）</summary>
    public NotificationType Type { get; init; } = NotificationType.System;

    /// <summary>模板编码（与 Title/Content 二选一；指定后自动渲染）</summary>
    public string? TemplateCode { get; init; }

    /// <summary>模板变量（TemplateCode 非空时使用）</summary>
    public Dictionary<string, object?>? TemplateData { get; init; }

    /// <summary>标题（TemplateCode 为空时必填）</summary>
    public string? Title { get; init; }

    /// <summary>内容（TemplateCode 为空时必填）</summary>
    public string? Content { get; init; }

    /// <summary>业务类型编码（如 ORDER_PAID，可选）</summary>
    public string? BizType { get; init; }

    /// <summary>业务单据 ID（如订单号，可选）</summary>
    public string? BizId { get; init; }

    /// <summary>发送后是否实时推送（默认 true，配合 SignalR）</summary>
    public bool Realtime { get; init; } = true;
}

/// <summary>发送短信请求（内部接口）</summary>
public sealed record SendSmsRequest
{
    /// <summary>接收手机号（必填）</summary>
    public string Phone { get; init; } = string.Empty;

    /// <summary>短信内容（必填）</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>最大重试次数（默认 3）</summary>
    public int MaxRetryCount { get; init; } = 3;
}

/// <summary>发送 Push 请求（内部接口）</summary>
public sealed record SendPushRequest
{
    /// <summary>设备令牌（必填）</summary>
    public string DeviceToken { get; init; } = string.Empty;

    /// <summary>推送标题（必填）</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>推送内容（必填）</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>最大重试次数（默认 3）</summary>
    public int MaxRetryCount { get; init; } = 3;
}

/// <summary>站内信通知响应（用户端列表 / 实时推送共用）</summary>
public sealed record NotificationResponse
{
    /// <summary>通知 ID</summary>
    public Guid Id { get; init; }

    /// <summary>通知业务类型</summary>
    public NotificationType Type { get; init; }

    /// <summary>标题</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>内容</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>业务类型编码</summary>
    public string? BizType { get; init; }

    /// <summary>业务单据 ID</summary>
    public string? BizId { get; init; }

    /// <summary>是否已读</summary>
    public bool IsRead { get; init; }

    /// <summary>已读时间</summary>
    public DateTime? ReadAt { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>发送站内信结果（内部接口响应）</summary>
public sealed record SendInAppNotificationResponse
{
    /// <summary>通知 ID</summary>
    public Guid NotificationId { get; init; }

    /// <summary>是否实时送达（用户在线且 Realtime=true）</summary>
    public bool RealtimeDelivered { get; init; }
}

/// <summary>发送短信结果（内部接口响应）</summary>
public sealed record SendSmsResponse
{
    /// <summary>短信记录 ID</summary>
    public Guid SmsId { get; init; }

    /// <summary>发送状态</summary>
    public SmsStatus Status { get; init; }

    /// <summary>是否 DryRun 模拟（true 表示未真实下发）</summary>
    public bool DryRun { get; init; }
}

/// <summary>发送 Push 结果（内部接口响应）</summary>
public sealed record SendPushResponse
{
    /// <summary>推送记录 ID</summary>
    public Guid PushId { get; init; }

    /// <summary>推送状态</summary>
    public PushStatus Status { get; init; }

    /// <summary>是否 DryRun 模拟（true 表示未真实下发）</summary>
    public bool DryRun { get; init; }
}

/// <summary>未读通知统计响应</summary>
public sealed record UnreadCountResponse
{
    /// <summary>未读数量</summary>
    public int UnreadCount { get; init; }
}

/// <summary>保存通知模板请求（管理端）</summary>
public sealed record SaveNotificationTemplateRequest
{
    /// <summary>模板编码（唯一，如 ORDER_PAID）</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>标题模板（可含 {变量}）</summary>
    public string TitleTemplate { get; init; } = string.Empty;

    /// <summary>内容模板（可含 {变量}）</summary>
    public string BodyTemplate { get; init; } = string.Empty;

    /// <summary>适用渠道（位标志）</summary>
    public NotificationChannel Channels { get; init; } = NotificationChannel.InApp;

    /// <summary>模板说明（可选）</summary>
    public string? Description { get; init; }
}

/// <summary>通知模板响应（管理端）</summary>
public sealed record NotificationTemplateResponse
{
    /// <summary>模板 ID</summary>
    public Guid Id { get; init; }

    /// <summary>模板编码</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>标题模板</summary>
    public string TitleTemplate { get; init; } = string.Empty;

    /// <summary>内容模板</summary>
    public string BodyTemplate { get; init; } = string.Empty;

    /// <summary>适用渠道</summary>
    public NotificationChannel Channels { get; init; }

    /// <summary>模板说明</summary>
    public string? Description { get; init; }

    /// <summary>是否启用</summary>
    public bool IsActive { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>发布公告请求（平台 admin）</summary>
public sealed record PublishAnnouncementRequest
{
    /// <summary>公告标题（1-200 字符）</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>公告正文（1-5000 字符）</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>公告分类（默认系统公告）</summary>
    public AnnouncementCategory Category { get; init; } = AnnouncementCategory.System;
}

/// <summary>公告响应（列表 / 详情 / 实时推送共用；isRead 为当前用户已读状态）</summary>
public sealed record AnnouncementResponse
{
    /// <summary>公告 ID</summary>
    public Guid Id { get; init; }

    /// <summary>标题</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>正文</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>公告分类</summary>
    public AnnouncementCategory Category { get; init; }

    /// <summary>发布者名称</summary>
    public string PublisherName { get; init; } = string.Empty;

    /// <summary>公告状态</summary>
    public AnnouncementStatus Status { get; init; }

    /// <summary>发布时间</summary>
    public DateTime? PublishedAt { get; init; }

    /// <summary>当前用户是否已读</summary>
    public bool IsRead { get; init; }

    /// <summary>当前用户已读时间</summary>
    public DateTime? ReadAt { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

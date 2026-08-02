using System.ComponentModel.DataAnnotations;
using ImService.Domain.Enums;

namespace ImService.DTOs;

/// <summary>创建私聊会话请求（买家发起）</summary>
public sealed record CreatePrivateSessionRequest
{
    /// <summary>商户 ID（会话归属）</summary>
    [Required]
    public Guid MerchantId { get; init; }

    /// <summary>对方用户 ID（商户客服/员工）</summary>
    [Required]
    public Guid PeerUserId { get; init; }
}

/// <summary>创建群聊会话请求（商户端）</summary>
public sealed record CreateGroupSessionRequest
{
    /// <summary>群聊名称</summary>
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    /// <summary>客服成员用户 ID 列表（含发起人，自动去重）</summary>
    public List<Guid> StaffUserIds { get; init; } = [];
}

/// <summary>发送消息请求（REST 通道，Hub 走 Hub 方法）</summary>
public sealed record SendMessageRequest
{
    /// <summary>消息内容（文本 / 图片文件 URL / 订单卡片 JSON，最多 4000 字符）</summary>
    [Required, MaxLength(4000)]
    public string Content { get; init; } = string.Empty;

    /// <summary>消息类型（默认 Text）</summary>
    public ChatMessageType MessageType { get; init; } = ChatMessageType.Text;
}

/// <summary>内部推送请求（订单/物流状态等系统通知，X-Internal-Key 调用）</summary>
public sealed record PushNotificationRequest
{
    /// <summary>接收用户 ID</summary>
    [Required]
    public Guid ToUserId { get; init; }

    /// <summary>会话归属商户 ID（无会话时可定位/创建）</summary>
    [Required]
    public Guid MerchantId { get; init; }

    /// <summary>通知内容</summary>
    [Required, MaxLength(4000)]
    public string Content { get; init; } = string.Empty;

    /// <summary>消息类型（默认 System）</summary>
    public ChatMessageType MessageType { get; init; } = ChatMessageType.System;

    /// <summary>指定会话 ID（可选，缺省自动定位/创建）</summary>
    public Guid? SessionId { get; init; }
}

/// <summary>会话响应</summary>
public sealed record SessionResponse
{
    /// <summary>会话 ID</summary>
    public Guid Id { get; init; }

    /// <summary>商户 ID</summary>
    public Guid MerchantId { get; init; }

    /// <summary>会话类型（Private/Group）</summary>
    public ChatSessionType Type { get; init; }

    /// <summary>会话名称（群聊显示名）</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>会话状态（Active/Closed）</summary>
    public ChatSessionStatus Status { get; init; }

    /// <summary>最后一条消息时间（无消息为 null）</summary>
    public DateTime? LastMessageAt { get; init; }

    /// <summary>最后一条消息摘要</summary>
    public string LastMessagePreview { get; init; } = string.Empty;

    /// <summary>我的未读消息数</summary>
    public int UnreadCount { get; init; }

    /// <summary>成员列表</summary>
    public List<SessionMemberResponse> Members { get; init; } = [];

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>会话成员响应</summary>
public sealed record SessionMemberResponse
{
    /// <summary>用户 ID</summary>
    public Guid UserId { get; init; }

    /// <summary>用户名称</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>成员角色</summary>
    public ChatMemberRole Role { get; init; }
}

/// <summary>消息响应</summary>
public sealed record MessageResponse
{
    /// <summary>消息 ID</summary>
    public Guid Id { get; init; }

    /// <summary>所属会话 ID</summary>
    public Guid SessionId { get; init; }

    /// <summary>发送者用户 ID（系统消息为 Guid.Empty）</summary>
    public Guid SenderId { get; init; }

    /// <summary>发送者名称</summary>
    public string SenderName { get; init; } = string.Empty;

    /// <summary>发送者角色</summary>
    public ChatMemberRole SenderRole { get; init; }

    /// <summary>消息类型</summary>
    public ChatMessageType MessageType { get; init; }

    /// <summary>消息内容</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>是否已读</summary>
    public bool IsRead { get; init; }

    /// <summary>已读时间（未读为 null）</summary>
    public DateTime? ReadAt { get; init; }

    /// <summary>发送时间（UTC）</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>消息分页响应（游标式，倒序）</summary>
public sealed record MessagePageResponse
{
    /// <summary>消息列表（最新在前）</summary>
    public List<MessageResponse> Items { get; init; } = [];

    /// <summary>是否还有更早的消息</summary>
    public bool HasMore { get; init; }
}

/// <summary>已读回执响应</summary>
public sealed record ReadReceiptResponse
{
    /// <summary>会话 ID</summary>
    public Guid SessionId { get; init; }

    /// <summary>本次标记已读的消息数</summary>
    public int MarkedCount { get; init; }
}

/// <summary>内部推送响应</summary>
public sealed record PushNotificationResponse
{
    /// <summary>会话 ID（定位或新建）</summary>
    public Guid SessionId { get; init; }

    /// <summary>消息 ID</summary>
    public Guid MessageId { get; init; }

    /// <summary>用户是否在线（在线则已实时推送）</summary>
    public bool Delivered { get; init; }
}

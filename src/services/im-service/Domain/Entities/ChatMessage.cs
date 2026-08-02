using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Entities;
using ImService.Domain.Enums;

namespace ImService.Domain.Entities;

/// <summary>
/// 聊天消息 — 会话内的一条消息（文本/图片/文件/订单卡片/系统通知）。
/// 多租户：与所属会话相同的商户隔离。
/// 已读状态：IsRead=false 表示未读；接收方调用 MarkAsRead 后置位。
/// </summary>
public sealed class ChatMessage : MultiTenantEntity
{
    private ChatMessage() { } // EF Core

    /// <summary>创建消息（默认未读）</summary>
    /// <param name="merchantId">所属商户 ID</param>
    /// <param name="sessionId">所属会话 ID</param>
    /// <param name="senderId">发送者用户 ID（系统消息可为 Guid.Empty）</param>
    /// <param name="senderName">发送者名称（快照）</param>
    /// <param name="senderRole">发送者角色</param>
    /// <param name="messageType">消息类型</param>
    /// <param name="content">消息内容（文本/URL/卡片 JSON）</param>
    /// <param name="now">发送时间（UTC）</param>
    [SetsRequiredMembers]
    public ChatMessage(
        Guid merchantId, Guid sessionId, Guid senderId, string senderName,
        ChatMemberRole senderRole, ChatMessageType messageType, string content, DateTime now)
    {
        MerchantId = merchantId;
        SessionId = sessionId;
        SenderId = senderId;
        SenderName = (senderName ?? string.Empty).Trim();
        SenderRole = senderRole;
        MessageType = messageType;
        Content = (content ?? string.Empty).Trim();
        CreatedAt = now;
    }

    /// <summary>所属会话 ID</summary>
    public Guid SessionId { get; private set; }

    /// <summary>发送者用户 ID（系统消息为 Guid.Empty）</summary>
    public Guid SenderId { get; private set; }

    /// <summary>发送者名称（快照）</summary>
    public string SenderName { get; private set; } = string.Empty;

    /// <summary>发送者角色</summary>
    public ChatMemberRole SenderRole { get; private set; }

    /// <summary>消息类型</summary>
    public ChatMessageType MessageType { get; private set; }

    /// <summary>消息内容（文本 / 图片文件 URL / 订单卡片 JSON / 系统通知文本）</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>是否已读（false=未读）</summary>
    public bool IsRead { get; private set; }

    /// <summary>已读时间（未读为 null）</summary>
    public DateTime? ReadAt { get; private set; }

    /// <summary>标记已读（幂等，仅未读时置位）</summary>
    /// <param name="now">已读时间（UTC）</param>
    /// <returns>是否发生状态变更</returns>
    public bool MarkRead(DateTime now)
    {
        if (IsRead)
            return false;

        IsRead = true;
        ReadAt = now;
        return true;
    }
}

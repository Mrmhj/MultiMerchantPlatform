using ImService.Domain.Entities;
using ImService.DTOs;

namespace ImService.Application;

/// <summary>
/// 实体 → DTO 映射。
/// </summary>
public static class ImMapper
{
    /// <summary>会话实体转响应 DTO</summary>
    /// <param name="session">会话实体</param>
    /// <param name="unreadCount">我的未读消息数</param>
    /// <returns>会话响应</returns>
    public static SessionResponse ToSessionResponse(ChatSession session, int unreadCount = 0) => new()
    {
        Id = session.Id,
        MerchantId = session.MerchantId,
        Type = session.Type,
        Name = session.Name,
        Status = session.Status,
        LastMessageAt = session.LastMessageAt,
        LastMessagePreview = session.LastMessagePreview,
        UnreadCount = unreadCount,
        Members = session.Members.Select(ToMemberResponse).ToList(),
        CreatedAt = session.CreatedAt,
    };

    /// <summary>会话成员实体转响应 DTO</summary>
    /// <param name="member">成员实体</param>
    /// <returns>成员响应</returns>
    public static SessionMemberResponse ToMemberResponse(ChatSessionMember member) => new()
    {
        UserId = member.UserId,
        UserName = member.UserName,
        Role = member.Role,
    };

    /// <summary>消息实体转响应 DTO</summary>
    /// <param name="message">消息实体</param>
    /// <returns>消息响应</returns>
    public static MessageResponse ToMessageResponse(ChatMessage message) => new()
    {
        Id = message.Id,
        SessionId = message.SessionId,
        SenderId = message.SenderId,
        SenderName = message.SenderName,
        SenderRole = message.SenderRole,
        MessageType = message.MessageType,
        Content = message.Content,
        IsRead = message.IsRead,
        ReadAt = message.ReadAt,
        CreatedAt = message.CreatedAt,
    };
}

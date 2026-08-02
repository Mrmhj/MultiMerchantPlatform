using ImService.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace ImService.Application.Hubs;

/// <summary>
/// 消息分发器 — Hub 与 REST 控制器共用的 SignalR 推送通道（强类型客户端 <see cref="IChatClient"/>）。
/// 会话推送按 Group（group 名 = sessionId），内部系统推送按 User（UserId）。
/// </summary>
public sealed class MessageDispatcher(
    IHubContext<ChatHub, IChatClient> hubContext,
    UserConnectionManager connectionManager)
{
    /// <summary>向会话全部在线成员广播新消息（含发送者自己，前端按 SenderId 区分左右）</summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="message">消息 DTO</param>
    public Task SendToSessionAsync(Guid sessionId, MessageResponse message)
        => hubContext.Clients.Group(sessionId.ToString()).ReceiveMessage(message);

    /// <summary>向指定用户全部在线连接推送消息（系统通知/内部推送）</summary>
    /// <param name="userId">目标用户 ID</param>
    /// <param name="message">消息 DTO</param>
    public Task SendToUserAsync(Guid userId, MessageResponse message)
        => hubContext.Clients.User(userId.ToString()).ReceiveMessage(message);

    /// <summary>广播已读回执（除执行者外的会话成员收到）</summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="readerUserId">执行已读的用户 ID</param>
    /// <param name="markedCount">本次标记已读的消息数</param>
    public Task NotifyReadAsync(Guid sessionId, Guid readerUserId, int markedCount)
        => hubContext.Clients.Group(sessionId.ToString()).MessageRead(sessionId, readerUserId, markedCount);

    /// <summary>转发输入中指示（排除发送者自己）</summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="senderId">输入中的用户 ID</param>
    /// <param name="senderName">输入中的用户名称</param>
    /// <param name="exceptConnectionId">排除的连接 ID（发送者本人）</param>
    public Task NotifyTypingAsync(Guid sessionId, Guid senderId, string senderName, string exceptConnectionId)
        => hubContext.Clients.GroupExcept(sessionId.ToString(), exceptConnectionId)
            .TypingIndicator(sessionId, senderId, senderName);

    /// <summary>用户是否在线（内部推送是否实时送达）</summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>true 表示在线</returns>
    public bool IsOnline(Guid userId) => connectionManager.IsOnline(userId);
}

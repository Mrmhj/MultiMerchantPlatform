using NotificationService.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace NotificationService.Application.Hubs;

/// <summary>
/// 通知分发器 — REST 控制器/命令处理器与 SignalR Hub 共用的实时推送通道（强类型客户端 <see cref="INotificationClient"/>）。
/// 按用户定向推送（SignalR UserIdentifier = JWT sub）。
/// </summary>
public sealed class NotificationDispatcher(IHubContext<NotificationHub, INotificationClient> hubContext)
{
    /// <summary>向指定用户全部在线连接推送新通知</summary>
    /// <param name="userId">目标用户 ID</param>
    /// <param name="notification">通知 DTO</param>
    public Task PushAsync(Guid userId, NotificationResponse notification)
        => hubContext.Clients.User(userId.ToString()).ReceiveNotification(notification);

    /// <summary>向指定用户推送未读数变化（标记已读后同步角标）</summary>
    /// <param name="userId">目标用户 ID</param>
    /// <param name="unreadCount">最新未读数</param>
    public Task NotifyUnreadAsync(Guid userId, int unreadCount)
        => hubContext.Clients.User(userId.ToString()).UnreadCountChanged(unreadCount);
}

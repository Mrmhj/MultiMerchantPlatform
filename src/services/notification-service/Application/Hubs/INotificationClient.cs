using NotificationService.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace NotificationService.Application.Hubs;

/// <summary>
/// SignalR 强类型客户端接口 — 通知服务端 → 客户端的推送方法集合。
/// Web / Electron 端用 <c>@microsoft/signalr</c>，uni-app 用 SignalR（H5/小程序）+ 原生 WebSocket（App 端）。
/// </summary>
public interface INotificationClient
{
    /// <summary>收到新通知（实时推送；客户端据此刷新收件箱/未读角标）</summary>
    /// <param name="notification">通知 DTO</param>
    Task ReceiveNotification(NotificationResponse notification);

    /// <summary>未读数变化（标记已读后客户端同步角标）</summary>
    /// <param name="unreadCount">最新未读数</param>
    Task UnreadCountChanged(int unreadCount);
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace NotificationService.Application.Hubs;

/// <summary>
/// 通知实时推送 Hub — 客户端连接后按 JWT 用户身份（sub）接收新通知与未读数变化。
/// 连接地址：/hub/notification（WebSocket 场景令牌通过 query access_token 携带）。
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub<INotificationClient>
{
    // 无自定义方法：连接即订阅，服务端经 IHubContext 按 UserId 定向推送。
}

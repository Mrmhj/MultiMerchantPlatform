using ImService.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace ImService.Application.Hubs;

/// <summary>
/// SignalR 强类型客户端接口 — 服务端 → 客户端的推送方法集合。
/// Web / Electron 端用 <c>@microsoft/signalr</c>，uni-app 用 SignalR（H5/小程序）+ 原生 WebSocket（App 端）。
/// </summary>
public interface IChatClient
{
    /// <summary>收到新消息（实时推送 / 上线补推离线消息均走此通道）</summary>
    /// <param name="message">消息 DTO</param>
    Task ReceiveMessage(MessageResponse message);

    /// <summary>会话已读回执（对方已读了我的消息）</summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="readerUserId">执行已读的用户 ID</param>
    /// <param name="markedCount">本次标记已读的消息数</param>
    Task MessageRead(Guid sessionId, Guid readerUserId, int markedCount);

    /// <summary>输入中指示（转发给会话内其他在线成员）</summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="senderId">输入中的用户 ID</param>
    /// <param name="senderName">输入中的用户名称</param>
    Task TypingIndicator(Guid sessionId, Guid senderId, string senderName);
}

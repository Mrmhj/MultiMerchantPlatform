using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BuildingBlocks.Core.CQRS;
using ImService.Application.Commands;
using ImService.Application.Queries;
using ImService.Domain.Enums;
using ImService.DTOs;
using ImService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ImService.Application.Hubs;

/// <summary>
/// 即时通讯 Hub（路径 /hub/chat，JWT 鉴权，WebSocket 通过 query access_token 携带令牌）。
/// 能力：上线注册（加入会话组 + 补推离线消息）、发送消息（私聊/群聊统一按会话）、
/// 已读回执、输入中指示、下线清理。
/// </summary>
[Authorize]
public sealed class ChatHub(
    MessageDispatcher dispatcher,
    UserConnectionManager connectionManager,
    IServiceScopeFactory scopeFactory,
    ILogger<ChatHub> logger) : Hub<IChatClient>
{
    /// <summary>当前用户 ID（由 <see cref="CustomUserIdProvider"/> 从 JWT sub 解析）</summary>
    private Guid? UserId => Guid.TryParse(Context.UserIdentifier, out var id) ? id : null;

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var userId = UserId;
        if (!userId.HasValue)
            throw new HubException("用户身份无效");

        connectionManager.OnConnected(userId.Value, Context.ConnectionId);
        logger.LogInformation("用户 {UserId} 上线（连接 {ConnectionId}）", userId.Value, Context.ConnectionId);

        // 加入该用户全部活跃会话组（群聊/私聊统一按会话 Group 广播）
        await JoinSessionGroupsAsync(userId.Value, CancellationToken.None);
        // 补推离线（未读）消息
        await PushOfflineMessagesAsync(userId.Value, CancellationToken.None);

        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (UserId is { } userId)
        {
            connectionManager.OnDisconnected(userId, Context.ConnectionId);
            logger.LogInformation("用户 {UserId} 下线（连接 {ConnectionId}）", userId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>发送消息（私聊/群聊统一入口）：落库 → 广播给会话组 → 返回消息 DTO</summary>
    /// <param name="sessionId">会话 ID</param>
    /// <param name="content">消息内容</param>
    /// <param name="messageType">消息类型（默认 Text）</param>
    /// <returns>落库后的消息 DTO（调用方 await invoke 获得）</returns>
    public async Task<MessageResponse> SendMessage(Guid sessionId, string content, ChatMessageType messageType = ChatMessageType.Text)
    {
        var userId = UserId ?? throw new HubException("用户身份无效");
        var userName = Context.User?.FindFirstValue(JwtRegisteredClaimNames.UniqueName) ?? string.Empty;

        using var scope = scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var message = await mediator.SendAsync<SendMessageCommand, MessageResponse>(new(
            sessionId, userId, userName, ResolveMemberRole(), content, messageType), CancellationToken.None);

        await dispatcher.SendToSessionAsync(sessionId, message);
        return message;
    }

    /// <summary>标记会话全部已读（广播已读回执给会话组）</summary>
    /// <param name="sessionId">会话 ID</param>
    public async Task MarkAsRead(Guid sessionId)
    {
        var userId = UserId ?? throw new HubException("用户身份无效");

        using var scope = scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var receipt = await mediator.SendAsync<MarkSessionReadCommand, ReadReceiptResponse>(
            new(sessionId, userId), CancellationToken.None);

        await dispatcher.NotifyReadAsync(sessionId, userId, receipt.MarkedCount);
    }

    /// <summary>输入中指示（转发给会话内其他在线成员，不落库）</summary>
    /// <param name="sessionId">会话 ID</param>
    public Task SendTypingIndicator(Guid sessionId)
    {
        var userId = UserId ?? throw new HubException("用户身份无效");
        var userName = Context.User?.FindFirstValue(JwtRegisteredClaimNames.UniqueName) ?? string.Empty;
        return dispatcher.NotifyTypingAsync(sessionId, userId, userName, Context.ConnectionId);
    }

    /// <summary>加入该用户全部活跃会话组</summary>
    private async Task JoinSessionGroupsAsync(Guid userId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ImDbContext>();

        var sessionIds = await db.ChatSessionMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.SessionId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var sessionId in sessionIds)
            await Groups.AddToGroupAsync(Context.ConnectionId, sessionId.ToString(), ct);
    }

    /// <summary>补推离线（未读且非自己发送）的最近消息，最多 50 条；仅限该用户参与会话内的消息</summary>
    private async Task PushOfflineMessagesAsync(Guid userId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ImDbContext>();

        // 该用户参与的会话（成员校验，防止越权读到其他会话未读）
        var sessionIds = await db.ChatSessionMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.SessionId)
            .Distinct()
            .ToListAsync(ct);
        if (sessionIds.Count == 0)
            return;

        var unread = await db.ChatMessages.AsNoTracking()
            .Where(m => sessionIds.Contains(m.SessionId) && m.SenderId != userId && !m.IsRead)
            .OrderByDescending(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var message in unread)
            await Clients.Caller.ReceiveMessage(ImMapper.ToMessageResponse(message));

        if (unread.Count > 0)
            logger.LogInformation("用户 {UserId} 上线补推 {Count} 条离线消息", userId, unread.Count);
    }

    /// <summary>从 JWT 推断成员角色：admin → Admin；带 merchant_id → MerchantStaff；其余 → Buyer</summary>
    private ChatMemberRole ResolveMemberRole()
    {
        if (Context.User?.IsInRole("admin") == true)
            return ChatMemberRole.Admin;
        if (Context.User?.FindFirstValue("merchant_id") is { Length: > 0 })
            return ChatMemberRole.MerchantStaff;
        return ChatMemberRole.Buyer;
    }
}

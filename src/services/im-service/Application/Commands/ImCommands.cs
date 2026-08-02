using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using ImService.Domain.Entities;
using ImService.Domain.Enums;
using ImService.DTOs;
using ImService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ImService.Application.Commands;

/// <summary>获取或创建私聊会话命令（买家发起：买家 ↔ 商户客服）</summary>
/// <param name="UserId">当前买家用户 ID</param>
/// <param name="UserName">当前买家名称</param>
/// <param name="MerchantId">商户 ID（会话归属）</param>
/// <param name="PeerUserId">对方用户 ID（商户客服/员工）</param>
/// <param name="PeerUserName">对方名称（可选，缺省用历史记录或空串）</param>
public sealed record GetOrCreatePrivateSessionCommand(
    Guid UserId, string UserName, Guid MerchantId, Guid PeerUserId, string? PeerUserName)
    : ICommand<SessionResponse>;

/// <summary>获取或创建私聊会话命令处理器：双向查找已有活跃会话，无则创建并加入双方成员。</summary>
public sealed class GetOrCreatePrivateSessionCommandHandler(
    ImDbContext db) : ICommandHandler<GetOrCreatePrivateSessionCommand, SessionResponse>
{
    /// <inheritdoc />
    public async Task<SessionResponse> HandleAsync(GetOrCreatePrivateSessionCommand command, CancellationToken ct = default)
    {
        if (command.PeerUserId == command.UserId)
            throw new DomainException("不能与自己创建会话", "INVALID_PEER");

        // 1. 双向查找已有活跃私聊会话（防止重复创建）
        var existing = await db.ChatSessions
            .Include(s => s.Members)
            .Where(s => s.MerchantId == command.MerchantId
                && s.Type == ChatSessionType.Private
                && s.Status == ChatSessionStatus.Active
                && s.Members.Any(m => m.UserId == command.UserId)
                && s.Members.Any(m => m.UserId == command.PeerUserId))
            .OrderByDescending(s => s.LastMessageAt)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
            return ImMapper.ToSessionResponse(existing);

        // 2. 对方名称：优先请求传入，其次复用历史成员快照
        var peerName = (command.PeerUserName ?? string.Empty).Trim();
        if (peerName.Length == 0)
        {
            peerName = await db.ChatSessionMembers.AsNoTracking()
                .Where(m => m.UserId == command.PeerUserId)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => m.UserName)
                .FirstOrDefaultAsync(ct) ?? string.Empty;
        }

        // 3. 创建会话 + 双方成员
        var now = DateTime.UtcNow;
        var session = new ChatSession(command.MerchantId, ChatSessionType.Private, null, now);
        session.AddMember(command.UserId, (command.UserName ?? string.Empty).Trim(), ChatMemberRole.Buyer);
        session.AddMember(command.PeerUserId, peerName, ChatMemberRole.MerchantStaff);

        db.ChatSessions.Add(session);
        await db.SaveChangesAsync(ct);

        return ImMapper.ToSessionResponse(session);
    }
}

/// <summary>创建群聊会话命令（商户端：客服群，自动加入发起人与客服成员）</summary>
/// <param name="MerchantId">商户 ID（X-Merchant-Id）</param>
/// <param name="Name">群聊名称</param>
/// <param name="StaffUserIds">客服成员用户 ID 列表</param>
/// <param name="CreatorId">发起人用户 ID</param>
/// <param name="CreatorName">发起人名称</param>
public sealed record CreateGroupSessionCommand(
    Guid MerchantId, string Name, List<Guid> StaffUserIds, Guid CreatorId, string CreatorName)
    : ICommand<SessionResponse>;

/// <summary>创建群聊会话命令处理器：去重加入成员，成员角色均为商户客服。</summary>
public sealed class CreateGroupSessionCommandHandler(
    ImDbContext db) : ICommandHandler<CreateGroupSessionCommand, SessionResponse>
{
    /// <inheritdoc />
    public async Task<SessionResponse> HandleAsync(CreateGroupSessionCommand command, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var session = new ChatSession(command.MerchantId, ChatSessionType.Group, command.Name, now);

        // 发起人 + 客服成员（自动去重）
        session.AddMember(command.CreatorId, (command.CreatorName ?? string.Empty).Trim(), ChatMemberRole.MerchantStaff);
        foreach (var staffId in command.StaffUserIds.Distinct())
        {
            if (staffId == command.CreatorId)
                continue;
            var staffName = await db.ChatSessionMembers.AsNoTracking()
                .Where(m => m.UserId == staffId)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => m.UserName)
                .FirstOrDefaultAsync(ct) ?? string.Empty;
            session.AddMember(staffId, staffName, ChatMemberRole.MerchantStaff);
        }

        if (session.Members.Count < 2)
            throw new DomainException("群聊至少需要 2 名成员", "GROUP_MEMBERS_REQUIRED");

        db.ChatSessions.Add(session);
        await db.SaveChangesAsync(ct);

        return ImMapper.ToSessionResponse(session);
    }
}

/// <summary>发送消息命令（Hub 与 REST 共用通道，落库后由调用方推送）</summary>
/// <param name="SessionId">会话 ID</param>
/// <param name="SenderId">发送者用户 ID（系统消息为 Guid.Empty）</param>
/// <param name="SenderName">发送者名称（快照）</param>
/// <param name="SenderRole">发送者角色</param>
/// <param name="Content">消息内容</param>
/// <param name="MessageType">消息类型</param>
public sealed record SendMessageCommand(
    Guid SessionId, Guid SenderId, string SenderName, ChatMemberRole SenderRole, string Content, ChatMessageType MessageType)
    : ICommand<MessageResponse>;

/// <summary>发送消息命令处理器：校验会话与成员 → 落库 → 更新会话摘要。</summary>
public sealed class SendMessageCommandHandler(
    ImDbContext db,
    ILogger<SendMessageCommandHandler> logger) : ICommandHandler<SendMessageCommand, MessageResponse>
{
    /// <inheritdoc />
    public async Task<MessageResponse> HandleAsync(SendMessageCommand command, CancellationToken ct = default)
    {
        var content = (command.Content ?? string.Empty).Trim();
        if (content.Length == 0)
            throw new DomainException("消息内容不能为空", "MESSAGE_CONTENT_REQUIRED");
        if (content.Length > 4000)
            throw new DomainException("消息内容不能超过 4000 字符", "MESSAGE_TOO_LONG");

        var session = await db.ChatSessions
            .Include(s => s.Members)
            .FirstOrDefaultAsync(s => s.Id == command.SessionId, ct)
            ?? throw new NotFoundException("会话", command.SessionId);

        if (session.Status != ChatSessionStatus.Active)
            throw new DomainException("会话已关闭，不能发送消息", "SESSION_CLOSED");
        if (command.SenderId != Guid.Empty && !session.ContainsMember(command.SenderId))
            throw new DomainException("不是会话成员，不能发送消息", "NOT_SESSION_MEMBER");

        // 发送者角色以会话成员表为权威（Hub 的 JWT 推断可能不准确，如未提权的客服）
        var senderRole = command.SenderRole;
        if (command.SenderId != Guid.Empty)
        {
            var member = session.Members.FirstOrDefault(m => m.UserId == command.SenderId);
            if (member is not null)
                senderRole = member.Role;
        }

        var now = DateTime.UtcNow;
        var message = new ChatMessage(
            session.MerchantId, session.Id, command.SenderId,
            (command.SenderName ?? string.Empty).Trim(), senderRole,
            command.MessageType, content, now);

        // 显式标记 Added（充血模型下避免 EF 将新建实体推断为 Unchanged）
        db.ChatMessages.Add(message);
        session.TouchMessage(content, now);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("会话 {SessionId} 收到新消息（类型 {Type}，发送者 {SenderId}）", session.Id, command.MessageType, command.SenderId);

        return ImMapper.ToMessageResponse(message);
    }
}

/// <summary>标记会话已读命令（接收方将本会话全部未读消息置为已读）</summary>
/// <param name="SessionId">会话 ID</param>
/// <param name="UserId">当前用户 ID（仅标记他人发来的消息）</param>
public sealed record MarkSessionReadCommand(Guid SessionId, Guid UserId) : ICommand<ReadReceiptResponse>;

/// <summary>标记会话已读命令处理器：校验成员 → 批量置为已读 → 返回本次标记数。</summary>
public sealed class MarkSessionReadCommandHandler(
    ImDbContext db) : ICommandHandler<MarkSessionReadCommand, ReadReceiptResponse>
{
    /// <inheritdoc />
    public async Task<ReadReceiptResponse> HandleAsync(MarkSessionReadCommand command, CancellationToken ct = default)
    {
        var session = await db.ChatSessions
            .Include(s => s.Members)
            .FirstOrDefaultAsync(s => s.Id == command.SessionId, ct)
            ?? throw new NotFoundException("会话", command.SessionId);

        if (!session.ContainsMember(command.UserId))
            throw new DomainException("不是会话成员", "NOT_SESSION_MEMBER");

        var unread = await db.ChatMessages
            .Where(m => m.SessionId == command.SessionId && !m.IsRead && m.SenderId != command.UserId)
            .ToListAsync(ct);

        if (unread.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var message in unread)
                message.MarkRead(now);
            await db.SaveChangesAsync(ct);
        }

        return new ReadReceiptResponse { SessionId = command.SessionId, MarkedCount = unread.Count };
    }
}

/// <summary>内部系统通知推送命令（订单/物流状态等，X-Internal-Key 调用）</summary>
/// <param name="ToUserId">接收用户 ID</param>
/// <param name="MerchantId">会话归属商户 ID</param>
/// <param name="Content">通知内容</param>
/// <param name="MessageType">消息类型（默认 System）</param>
/// <param name="SessionId">指定会话 ID（可选，缺省自动定位/创建）</param>
public sealed record PushNotificationCommand(
    Guid ToUserId, Guid MerchantId, string Content, ChatMessageType MessageType, Guid? SessionId)
    : ICommand<PushNotificationResponse>;

/// <summary>
/// 内部系统通知推送命令处理器：
/// 定位会话（指定 → 用户在该商户最近活跃会话 → 新建系统会话）→ 落库系统消息 → 更新会话摘要。
/// 实时推送由调用方（Controller）通过 MessageDispatcher 完成。
/// </summary>
public sealed class PushNotificationCommandHandler(
    ImDbContext db) : ICommandHandler<PushNotificationCommand, PushNotificationResponse>
{
    /// <inheritdoc />
    public async Task<PushNotificationResponse> HandleAsync(PushNotificationCommand command, CancellationToken ct = default)
    {
        var content = (command.Content ?? string.Empty).Trim();
        if (content.Length == 0)
            throw new DomainException("通知内容不能为空", "MESSAGE_CONTENT_REQUIRED");

        ChatSession session;

        // 1. 定位会话
        if (command.SessionId.HasValue)
        {
            session = await db.ChatSessions
                .Include(s => s.Members)
                .FirstOrDefaultAsync(s => s.Id == command.SessionId.Value, ct)
                ?? throw new NotFoundException("会话", command.SessionId.Value);
        }
        else
        {
            session = await db.ChatSessions
                .Include(s => s.Members)
                .Where(s => s.MerchantId == command.MerchantId
                    && s.Status == ChatSessionStatus.Active
                    && s.Members.Any(m => m.UserId == command.ToUserId))
                .OrderByDescending(s => s.LastMessageAt)
                .FirstOrDefaultAsync(ct)
                ?? await CreateSystemSessionAsync(command.ToUserId, command.MerchantId, ct);
        }

        // 2. 落库系统消息
        var now = DateTime.UtcNow;
        var message = new ChatMessage(
            session.MerchantId, session.Id, Guid.Empty, "系统通知",
            ChatMemberRole.System, command.MessageType, content, now);

        db.ChatMessages.Add(message);
        session.TouchMessage(content, now);
        await db.SaveChangesAsync(ct);

        return new PushNotificationResponse { SessionId = session.Id, MessageId = message.Id };
    }

    /// <summary>创建系统通知会话（单成员：接收用户）</summary>
    private async Task<ChatSession> CreateSystemSessionAsync(Guid toUserId, Guid merchantId, CancellationToken ct)
    {
        // 复用该用户已有名称快照，避免显示占位名
        var userName = await db.ChatSessionMembers.AsNoTracking()
            .Where(m => m.UserId == toUserId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => m.UserName)
            .FirstOrDefaultAsync(ct) ?? "用户";

        var session = new ChatSession(merchantId, ChatSessionType.Private, "系统通知", DateTime.UtcNow);
        session.AddMember(toUserId, userName, ChatMemberRole.Buyer);
        db.ChatSessions.Add(session);
        return session;
    }
}

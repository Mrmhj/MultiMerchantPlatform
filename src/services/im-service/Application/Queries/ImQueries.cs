using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using ImService.Domain.Enums;
using ImService.DTOs;
using ImService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ImService.Application.Queries;

/// <summary>我的会话列表查询（C 端买家，按当前用户参与会话过滤）</summary>
/// <param name="UserId">当前用户 ID</param>
public sealed record MySessionsQuery(Guid UserId) : IQuery<List<SessionResponse>>;

/// <summary>我的会话列表查询处理器（含未读数，最新会话在前）</summary>
public sealed class MySessionsQueryHandler(
    ImDbContext db) : IQueryHandler<MySessionsQuery, List<SessionResponse>>
{
    /// <inheritdoc />
    public async Task<List<SessionResponse>> HandleAsync(MySessionsQuery query, CancellationToken ct = default)
    {
        var sessionIds = await db.ChatSessionMembers.AsNoTracking()
            .Where(m => m.UserId == query.UserId)
            .Select(m => m.SessionId)
            .Distinct()
            .ToListAsync(ct);
        if (sessionIds.Count == 0)
            return [];

        var sessions = await db.ChatSessions.AsNoTracking()
            .Include(s => s.Members)
            .Where(s => sessionIds.Contains(s.Id) && s.Status == ChatSessionStatus.Active)
            .OrderByDescending(s => s.LastMessageAt)
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        var unread = await db.ChatMessages.AsNoTracking()
            .Where(m => sessionIds.Contains(m.SessionId) && !m.IsRead && m.SenderId != query.UserId)
            .GroupBy(m => m.SessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count, ct);

        return sessions
            .Select(s => ImMapper.ToSessionResponse(s, unread.GetValueOrDefault(s.Id)))
            .ToList();
    }
}

/// <summary>商户会话列表查询（商户端，X-Merchant-Id + HasQueryFilter 双过滤）</summary>
/// <param name="MerchantId">商户 ID（X-Merchant-Id）</param>
public sealed record MerchantSessionsQuery(Guid MerchantId) : IQuery<List<SessionResponse>>;

/// <summary>商户会话列表查询处理器（含未读数，最新会话在前）</summary>
public sealed class MerchantSessionsQueryHandler(
    ImDbContext db) : IQueryHandler<MerchantSessionsQuery, List<SessionResponse>>
{
    /// <inheritdoc />
    public async Task<List<SessionResponse>> HandleAsync(MerchantSessionsQuery query, CancellationToken ct = default)
    {
        // HasQueryFilter（CurrentMerchantId）已按商户隔离，此处再显式过滤（多租户三重防护）
        var sessions = await db.ChatSessions.AsNoTracking()
            .Include(s => s.Members)
            .Where(s => s.MerchantId == query.MerchantId && s.Status == ChatSessionStatus.Active)
            .OrderByDescending(s => s.LastMessageAt)
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        var sessionIds = sessions.Select(s => s.Id).ToList();
        if (sessionIds.Count == 0)
            return [];

        // 商户视角：未读 = 买家/系统发来的消息（商户员工自己发送的不计入）
        var staffIds = await db.ChatSessionMembers.AsNoTracking()
            .Where(m => sessionIds.Contains(m.SessionId) && m.Role == ChatMemberRole.MerchantStaff)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(ct);

        var unread = await db.ChatMessages.AsNoTracking()
            .Where(m => sessionIds.Contains(m.SessionId) && !m.IsRead && !staffIds.Contains(m.SenderId))
            .GroupBy(m => m.SessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SessionId, x => x.Count, ct);

        return sessions
            .Select(s => ImMapper.ToSessionResponse(s, unread.GetValueOrDefault(s.Id)))
            .ToList();
    }
}

/// <summary>会话消息分页查询（游标式，按 (CreatedAt, Id) 倒序）</summary>
/// <param name="SessionId">会话 ID</param>
/// <param name="UserId">当前用户 ID（成员校验）</param>
/// <param name="BeforeId">游标：仅返回早于该消息的消息（可选，缺省最新一页）</param>
/// <param name="Limit">每页条数（默认 50，上限 200）</param>
/// <param name="MerchantId">商户 ID（可选，商户端显式校验）</param>
public sealed record SessionMessagesQuery(
    Guid SessionId, Guid UserId, Guid? BeforeId, int Limit, Guid? MerchantId = null)
    : IQuery<MessagePageResponse>;

/// <summary>会话消息分页查询处理器（成员校验 + 游标分页）</summary>
public sealed class SessionMessagesQueryHandler(
    ImDbContext db) : IQueryHandler<SessionMessagesQuery, MessagePageResponse>
{
    /// <inheritdoc />
    public async Task<MessagePageResponse> HandleAsync(SessionMessagesQuery query, CancellationToken ct = default)
    {
        var session = await db.ChatSessions
            .Include(s => s.Members)
            .FirstOrDefaultAsync(s => s.Id == query.SessionId, ct)
            ?? throw new NotFoundException("会话", query.SessionId);

        // 商户端显式校验商户归属（多租户三重防护）
        if (query.MerchantId.HasValue && session.MerchantId != query.MerchantId.Value)
            throw new NotFoundException("会话", query.SessionId);
        if (!session.ContainsMember(query.UserId))
            throw new DomainException("不是会话成员", "NOT_SESSION_MEMBER");

        var limit = Math.Clamp(query.Limit, 1, 200);
        var baseQuery = db.ChatMessages.AsNoTracking().Where(m => m.SessionId == query.SessionId);

        if (query.BeforeId.HasValue)
        {
            var cursor = await db.ChatMessages.AsNoTracking()
                .Where(m => m.Id == query.BeforeId.Value)
                .Select(m => new { m.CreatedAt })
                .FirstOrDefaultAsync(ct);
            if (cursor is null)
                throw new NotFoundException("消息", query.BeforeId.Value);

            // 游标字典序：早于 cursor 的消息
            baseQuery = baseQuery.Where(m =>
                m.CreatedAt < cursor.CreatedAt
                || (m.CreatedAt == cursor.CreatedAt && m.Id != query.BeforeId.Value));
        }

        // 多取一条判断是否还有更早的消息
        var page = await baseQuery
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(limit + 1)
            .ToListAsync(ct);

        var hasMore = page.Count > limit;
        var items = (hasMore ? page.Take(limit) : page)
            .Select(ImMapper.ToMessageResponse)
            .ToList();

        return new MessagePageResponse { Items = items, HasMore = hasMore };
    }
}

/// <summary>按消息 ID 查询消息（内部推送回读用）</summary>
/// <param name="MessageId">消息 ID</param>
public sealed record MessageByIdQuery(Guid MessageId) : IQuery<MessageResponse>;

/// <summary>按消息 ID 查询消息处理器</summary>
public sealed class MessageByIdQueryHandler(
    ImDbContext db) : IQueryHandler<MessageByIdQuery, MessageResponse>
{
    /// <inheritdoc />
    public async Task<MessageResponse> HandleAsync(MessageByIdQuery query, CancellationToken ct = default)
    {
        var message = await db.ChatMessages.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == query.MessageId, ct)
            ?? throw new NotFoundException("消息", query.MessageId);

        return ImMapper.ToMessageResponse(message);
    }
}

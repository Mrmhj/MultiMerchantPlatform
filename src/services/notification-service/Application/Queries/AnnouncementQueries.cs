using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using NotificationService.Domain.Enums;
using NotificationService.DTOs;
using NotificationService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NotificationService.Application.Queries;

/// <summary>公告分页查询（用户端）— 仅返回已发布公告，附带当前用户已读状态</summary>
/// <param name="UserId">当前用户 ID</param>
/// <param name="Category">按分类过滤（可选）</param>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
public sealed record AnnouncementListQuery(
    Guid UserId, AnnouncementCategory? Category, int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<AnnouncementResponse>>;

/// <summary>公告分页查询处理器（LEFT JOIN 已读记录，未读的 IsRead=false）</summary>
public sealed class AnnouncementListQueryHandler(
    NotificationDbContext db) : IQueryHandler<AnnouncementListQuery, PagedResult<AnnouncementResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<AnnouncementResponse>> HandleAsync(
        AnnouncementListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = db.Announcements.AsNoTracking()
            .Where(a => a.Status == AnnouncementStatus.Published);

        if (query.Category.HasValue)
            q = q.Where(a => a.Category == query.Category.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(a => a.PublishedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new { a, Read = db.AnnouncementReads
                .FirstOrDefault(ar => ar.AnnouncementId == a.Id && ar.UserId == query.UserId) })
            .ToListAsync(ct);

        return new PagedResult<AnnouncementResponse>(
            items.Select(x => NotificationMapper.ToAnnouncementResponse(
                x.a, isRead: x.Read is not null, readAt: x.Read?.ReadAt)).ToList(),
            total, page, pageSize);
    }
}

/// <summary>公告详情查询（用户端，含当前用户已读状态）</summary>
/// <param name="UserId">当前用户 ID</param>
/// <param name="AnnouncementId">公告 ID</param>
public sealed record AnnouncementByIdQuery(Guid UserId, Guid AnnouncementId)
    : IQuery<AnnouncementResponse>;

/// <summary>公告详情查询处理器</summary>
public sealed class AnnouncementByIdQueryHandler(
    NotificationDbContext db) : IQueryHandler<AnnouncementByIdQuery, AnnouncementResponse>
{
    /// <inheritdoc />
    public async Task<AnnouncementResponse> HandleAsync(
        AnnouncementByIdQuery query, CancellationToken ct = default)
    {
        var announcement = await db.Announcements.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == query.AnnouncementId, ct)
            ?? throw new NotFoundException("公告", query.AnnouncementId);
        if (!announcement.IsVisible)
            throw new DomainException("公告未发布或已下线", "ANNOUNCEMENT_NOT_AVAILABLE");

        var read = await db.AnnouncementReads.AsNoTracking()
            .FirstOrDefaultAsync(ar => ar.AnnouncementId == query.AnnouncementId && ar.UserId == query.UserId, ct);

        return NotificationMapper.ToAnnouncementResponse(
            announcement, isRead: read is not null, readAt: read?.ReadAt);
    }
}

/// <summary>公告未读数查询（用户端，顶栏角标）</summary>
/// <param name="UserId">当前用户 ID</param>
public sealed record AnnouncementUnreadCountQuery(Guid UserId) : IQuery<UnreadCountResponse>;

/// <summary>公告未读数查询处理器 — 已发布公告数 − 当前用户已读公告数</summary>
public sealed class AnnouncementUnreadCountQueryHandler(
    NotificationDbContext db) : IQueryHandler<AnnouncementUnreadCountQuery, UnreadCountResponse>
{
    /// <inheritdoc />
    public async Task<UnreadCountResponse> HandleAsync(
        AnnouncementUnreadCountQuery query, CancellationToken ct = default)
    {
        // 已发布且当前用户无已读记录的公告数（下线公告不计入）
        var unread = await db.Announcements.AsNoTracking()
            .Where(a => a.Status == AnnouncementStatus.Published)
            .Where(a => !db.AnnouncementReads.Any(
                ar => ar.AnnouncementId == a.Id && ar.UserId == query.UserId))
            .CountAsync(ct);
        return new UnreadCountResponse { UnreadCount = unread };
    }
}

using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Results;
using NotificationService.Domain.Enums;
using NotificationService.DTOs;
using NotificationService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NotificationService.Application.Queries;

/// <summary>我的通知分页查询（用户端）</summary>
/// <param name="UserId">当前用户 ID</param>
/// <param name="Type">按业务类型过滤（可选）</param>
/// <param name="IsRead">按已读状态过滤（可选）</param>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
public sealed record MyNotificationsQuery(
    Guid UserId, NotificationType? Type, bool? IsRead, int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<NotificationResponse>>;

/// <summary>我的通知分页查询处理器</summary>
public sealed class MyNotificationsQueryHandler(
    NotificationDbContext db) : IQueryHandler<MyNotificationsQuery, PagedResult<NotificationResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<NotificationResponse>> HandleAsync(
        MyNotificationsQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = db.Notifications.AsNoTracking()
            .Where(n => n.UserId == query.UserId && !n.IsDeleted);

        if (query.Type.HasValue)
            q = q.Where(n => n.Type == query.Type.Value);
        if (query.IsRead.HasValue)
            q = q.Where(n => n.IsRead == query.IsRead.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<NotificationResponse>(
            items.Select(NotificationMapper.ToResponse).ToList(), total, page, pageSize);
    }
}

/// <summary>未读通知数查询（用户端）</summary>
/// <param name="UserId">当前用户 ID</param>
public sealed record UnreadCountQuery(Guid UserId) : IQuery<UnreadCountResponse>;

/// <summary>未读通知数查询处理器</summary>
public sealed class UnreadCountQueryHandler(
    NotificationDbContext db) : IQueryHandler<UnreadCountQuery, UnreadCountResponse>
{
    /// <inheritdoc />
    public async Task<UnreadCountResponse> HandleAsync(UnreadCountQuery query, CancellationToken ct = default)
    {
        var count = await db.Notifications.AsNoTracking()
            .CountAsync(n => n.UserId == query.UserId && !n.IsRead && !n.IsDeleted, ct);
        return new UnreadCountResponse { UnreadCount = count };
    }
}

/// <summary>通知模板列表查询（管理端）</summary>
/// <param name="ActiveOnly">仅启用模板（可选）</param>
public sealed record NotificationTemplateListQuery(bool? ActiveOnly = null)
    : IQuery<IReadOnlyList<NotificationTemplateResponse>>;

/// <summary>通知模板列表查询处理器</summary>
public sealed class NotificationTemplateListQueryHandler(
    NotificationDbContext db) : IQueryHandler<NotificationTemplateListQuery, IReadOnlyList<NotificationTemplateResponse>>
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<NotificationTemplateResponse>> HandleAsync(
        NotificationTemplateListQuery query, CancellationToken ct = default)
    {
        var q = db.Templates.AsNoTracking().AsQueryable();
        if (query.ActiveOnly.HasValue)
            q = q.Where(t => t.IsActive == query.ActiveOnly.Value);

        var items = await q.OrderBy(t => t.Code).ToListAsync(ct);
        return items.Select(NotificationMapper.ToTemplateResponse).ToList();
    }
}

/// <summary>通知模板详情查询（管理端）</summary>
/// <param name="TemplateId">模板 ID</param>
public sealed record NotificationTemplateByIdQuery(Guid TemplateId)
    : IQuery<NotificationTemplateResponse>;

/// <summary>通知模板详情查询处理器</summary>
public sealed class NotificationTemplateByIdQueryHandler(
    NotificationDbContext db) : IQueryHandler<NotificationTemplateByIdQuery, NotificationTemplateResponse>
{
    /// <inheritdoc />
    public async Task<NotificationTemplateResponse> HandleAsync(
        NotificationTemplateByIdQuery query, CancellationToken ct = default)
    {
        var template = await db.Templates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == query.TemplateId, ct)
            ?? throw new BuildingBlocks.Core.Exceptions.NotFoundException("通知模板", query.TemplateId);
        return NotificationMapper.ToTemplateResponse(template);
    }
}

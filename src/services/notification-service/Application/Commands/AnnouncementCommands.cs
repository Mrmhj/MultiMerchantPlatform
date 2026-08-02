using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using NotificationService.Application.Hubs;
using NotificationService.Domain.Entities;
using NotificationService.DTOs;
using NotificationService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NotificationService.Application.Commands;

/// <summary>发布公告命令（平台 admin，创建即发布并广播）</summary>
/// <param name="Request">公告内容</param>
/// <param name="PublisherUserId">发布者用户 ID</param>
/// <param name="PublisherName">发布者名称</param>
public sealed record PublishAnnouncementCommand(
    PublishAnnouncementRequest Request, Guid PublisherUserId, string PublisherName)
    : ICommand<AnnouncementResponse>;

/// <summary>发布公告命令处理器 — 创建公告 → 发布 → 落库 → SignalR 全量广播</summary>
public sealed class PublishAnnouncementCommandHandler(
    NotificationDbContext db,
    NotificationDispatcher dispatcher,
    TimeProvider timeProvider) : ICommandHandler<PublishAnnouncementCommand, AnnouncementResponse>
{
    /// <inheritdoc />
    public async Task<AnnouncementResponse> HandleAsync(
        PublishAnnouncementCommand command, CancellationToken ct = default)
    {
        var r = command.Request;
        var announcement = new Announcement(
            r.Title, r.Content, r.Category, command.PublisherUserId, command.PublisherName);

        announcement.Publish(timeProvider.GetUtcNow().UtcDateTime);
        db.Announcements.Add(announcement);
        await db.SaveChangesAsync(ct);

        var response = NotificationMapper.ToAnnouncementResponse(announcement, isRead: false);
        // 广播给全部在线连接（离线用户下次登录从列表接口拉取）
        await dispatcher.BroadcastAnnouncementAsync(response);
        return response;
    }
}

/// <summary>下线公告命令（平台 admin）</summary>
/// <param name="AnnouncementId">公告 ID</param>
public sealed record OfflineAnnouncementCommand(Guid AnnouncementId) : ICommand<AnnouncementResponse>;

/// <summary>下线公告命令处理器</summary>
public sealed class OfflineAnnouncementCommandHandler(
    NotificationDbContext db, TimeProvider timeProvider) : ICommandHandler<OfflineAnnouncementCommand, AnnouncementResponse>
{
    /// <inheritdoc />
    public async Task<AnnouncementResponse> HandleAsync(
        OfflineAnnouncementCommand command, CancellationToken ct = default)
    {
        var announcement = await db.Announcements
            .FirstOrDefaultAsync(a => a.Id == command.AnnouncementId, ct)
            ?? throw new NotFoundException("公告", command.AnnouncementId);

        announcement.Offline(timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return NotificationMapper.ToAnnouncementResponse(announcement, isRead: false);
    }
}

/// <summary>标记公告已读命令（用户端，幂等 upsert）</summary>
/// <param name="UserId">当前用户 ID</param>
/// <param name="AnnouncementId">公告 ID</param>
public sealed record MarkAnnouncementReadCommand(Guid UserId, Guid AnnouncementId) : ICommand<AnnouncementResponse>;

/// <summary>标记公告已读命令处理器 — 校验公告已发布且存在，幂等写入已读记录</summary>
public sealed class MarkAnnouncementReadCommandHandler(
    NotificationDbContext db, TimeProvider timeProvider) : ICommandHandler<MarkAnnouncementReadCommand, AnnouncementResponse>
{
    /// <inheritdoc />
    public async Task<AnnouncementResponse> HandleAsync(
        MarkAnnouncementReadCommand command, CancellationToken ct = default)
    {
        var announcement = await db.Announcements.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == command.AnnouncementId, ct)
            ?? throw new NotFoundException("公告", command.AnnouncementId);
        if (!announcement.IsVisible)
            throw new DomainException("公告未发布或已下线", "ANNOUNCEMENT_NOT_AVAILABLE");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var existing = await db.AnnouncementReads
            .FirstOrDefaultAsync(ar => ar.AnnouncementId == command.AnnouncementId && ar.UserId == command.UserId, ct);

        if (existing is null)
        {
            db.AnnouncementReads.Add(new AnnouncementRead(command.AnnouncementId, command.UserId, now));
            await db.SaveChangesAsync(ct);
        }

        return NotificationMapper.ToAnnouncementResponse(announcement, isRead: true, readAt: now);
    }
}

using BuildingBlocks.Core.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

/// <summary>
/// 公告已读记录 — 用户维度标记某条公告已读（AnnouncementId + UserId 复合唯一）。
/// 广播模型下公告不复制到用户收件箱，已读状态按需惰性写入。
/// </summary>
public sealed class AnnouncementRead : Entity
{
    private AnnouncementRead() { } // EF Core

    /// <summary>标记公告已读</summary>
    /// <param name="announcementId">公告 ID</param>
    /// <param name="userId">用户 ID</param>
    /// <param name="readAt">已读时间</param>
    public AnnouncementRead(Guid announcementId, Guid userId, DateTime readAt)
    {
        AnnouncementId = announcementId;
        UserId = userId;
        ReadAt = readAt;
        CreatedAt = readAt;
    }

    /// <summary>公告 ID（外键 → Announcements.Id）</summary>
    public Guid AnnouncementId { get; private set; }

    /// <summary>用户 ID</summary>
    public Guid UserId { get; private set; }

    /// <summary>已读时间</summary>
    public DateTime ReadAt { get; private set; }
}

using NotificationService.Domain.Entities;
using NotificationService.DTOs;

namespace NotificationService.Application;

/// <summary>
/// 通知服务映射器 — 领域实体 → DTO。
/// </summary>
public static class NotificationMapper
{
    /// <summary>站内信实体 → 通知响应</summary>
    /// <param name="n">通知实体</param>
    /// <returns>通知 DTO</returns>
    public static NotificationResponse ToResponse(Notification n) => new()
    {
        Id = n.Id,
        Type = n.Type,
        Title = n.Title,
        Content = n.Content,
        BizType = n.BizType,
        BizId = n.BizId,
        IsRead = n.IsRead,
        ReadAt = n.ReadAt,
        CreatedAt = n.CreatedAt,
    };

    /// <summary>模板实体 → 模板响应</summary>
    /// <param name="t">模板实体</param>
    /// <returns>模板 DTO</returns>
    public static NotificationTemplateResponse ToTemplateResponse(NotificationTemplate t) => new()
    {
        Id = t.Id,
        Code = t.Code,
        TitleTemplate = t.TitleTemplate,
        BodyTemplate = t.BodyTemplate,
        Channels = t.Channels,
        Description = t.Description,
        IsActive = t.IsActive,
        CreatedAt = t.CreatedAt,
    };

    /// <summary>公告实体 → 公告响应（isRead/readAt 为当前用户已读状态）</summary>
    /// <param name="a">公告实体</param>
    /// <param name="isRead">当前用户是否已读</param>
    /// <param name="readAt">当前用户已读时间（可选）</param>
    /// <returns>公告 DTO</returns>
    public static AnnouncementResponse ToAnnouncementResponse(
        Announcement a, bool isRead, DateTime? readAt = null) => new()
        {
            Id = a.Id,
            Title = a.Title,
            Content = a.Content,
            Category = a.Category,
            PublisherName = a.PublisherName,
            Status = a.Status,
            PublishedAt = a.PublishedAt,
            IsRead = isRead,
            ReadAt = readAt,
            CreatedAt = a.CreatedAt,
        };
}

using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;
using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

/// <summary>
/// 平台公告 — 平台向全体用户广播的公告（一对多广播模型，不复制到各用户收件箱；
/// 已读状态单独记录在 <see cref="AnnouncementRead"/>）。
/// 与站内信（Notification，定向/一对一）互补：公告面向全员，站内信面向单用户。
/// </summary>
public sealed class Announcement : Entity, IAggregateRoot
{
    private Announcement() { } // EF Core

    /// <summary>创建公告（草稿态，发布时调用 <see cref="Publish"/>）</summary>
    /// <param name="title">标题（1-200 字符）</param>
    /// <param name="content">正文（1-5000 字符）</param>
    /// <param name="category">公告分类</param>
    /// <param name="publisherUserId">发布者用户 ID（平台管理员）</param>
    /// <param name="publisherName">发布者名称</param>
    public Announcement(
        string title, string content, AnnouncementCategory category,
        Guid publisherUserId, string publisherName)
    {
        Title = ValidateTitle(title);
        Content = ValidateContent(content);
        Category = category;
        PublisherUserId = publisherUserId;
        PublisherName = ValidatePublisherName(publisherName);
        Status = AnnouncementStatus.Draft;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>标题</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>正文</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>公告分类</summary>
    public AnnouncementCategory Category { get; private set; }

    /// <summary>发布者用户 ID（平台管理员）</summary>
    public Guid PublisherUserId { get; private set; }

    /// <summary>发布者名称</summary>
    public string PublisherName { get; private set; } = string.Empty;

    /// <summary>公告状态</summary>
    public AnnouncementStatus Status { get; private set; }

    /// <summary>发布时间（Status=Published 后非空）</summary>
    public DateTime? PublishedAt { get; private set; }

    /// <summary>下线时间（Status=Offline 后非空）</summary>
    public DateTime? OfflineAt { get; private set; }

    /// <summary>发布公告（草稿 → 已发布，幂等：已发布再次调用仅刷新发布时间）</summary>
    /// <param name="publishedAt">发布时间</param>
    public void Publish(DateTime publishedAt)
    {
        Status = AnnouncementStatus.Published;
        PublishedAt = publishedAt;
        UpdatedAt = publishedAt;
    }

    /// <summary>下线公告（已发布 → 已下线，幂等）</summary>
    /// <param name="offlineAt">下线时间</param>
    public void Offline(DateTime offlineAt)
    {
        if (Status == AnnouncementStatus.Offline) return;
        Status = AnnouncementStatus.Offline;
        OfflineAt = offlineAt;
        UpdatedAt = offlineAt;
    }

    /// <summary>是否用户可见（已发布）</summary>
    public bool IsVisible => Status == AnnouncementStatus.Published;

    private static string ValidateTitle(string title)
    {
        var trimmed = (title ?? string.Empty).Trim();
        if (trimmed.Length is < 1 or > 200)
            throw new DomainException("公告标题长度需在 1-200 字符之间", "INVALID_ANNOUNCEMENT_TITLE");
        return trimmed;
    }

    private static string ValidateContent(string content)
    {
        var trimmed = (content ?? string.Empty).Trim();
        if (trimmed.Length is < 1 or > 5000)
            throw new DomainException("公告内容长度需在 1-5000 字符之间", "INVALID_ANNOUNCEMENT_CONTENT");
        return trimmed;
    }

    private static string ValidatePublisherName(string publisherName)
    {
        var trimmed = (publisherName ?? string.Empty).Trim();
        if (trimmed.Length is < 1 or > 100)
            throw new DomainException("发布者名称长度需在 1-100 字符之间", "INVALID_ANNOUNCEMENT_PUBLISHER");
        return trimmed;
    }
}

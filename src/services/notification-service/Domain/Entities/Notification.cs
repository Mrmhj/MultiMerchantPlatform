using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;
using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

/// <summary>
/// 站内信通知 — 通知中心收件箱的一条记录（用户维度，MerchantId 为业务归属标记，平台级通知为空）。
/// </summary>
public sealed class Notification : Entity, IAggregateRoot
{
    private Notification() { } // EF Core

    /// <summary>创建通知</summary>
    /// <param name="userId">接收用户 ID</param>
    /// <param name="merchantId">业务归属商户 ID（平台级通知为空）</param>
    /// <param name="type">通知业务类型</param>
    /// <param name="title">标题</param>
    /// <param name="content">内容</param>
    /// <param name="bizType">业务类型编码（如 ORDER_PAID，可选）</param>
    /// <param name="bizId">业务单据 ID（如订单号，可选）</param>
    /// <param name="channel">来源渠道（默认站内信）</param>
    public Notification(
        Guid userId, Guid? merchantId, NotificationType type, string title, string content,
        string? bizType = null, string? bizId = null, NotificationChannel channel = NotificationChannel.InApp)
    {
        UserId = userId;
        MerchantId = merchantId;
        Type = type;
        Title = ValidateTitle(title);
        Content = ValidateContent(content);
        BizType = string.IsNullOrWhiteSpace(bizType) ? null : bizType.Trim();
        BizId = string.IsNullOrWhiteSpace(bizId) ? null : bizId.Trim();
        Channel = channel;
        IsRead = false;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>接收用户 ID</summary>
    public Guid UserId { get; private set; }

    /// <summary>业务归属商户 ID（平台级通知为空）</summary>
    public Guid? MerchantId { get; private set; }

    /// <summary>通知业务类型</summary>
    public NotificationType Type { get; private set; }

    /// <summary>标题</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>内容</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>业务类型编码（ORDER_PAID / ORDER_SHIPPED 等，可选）</summary>
    public string? BizType { get; private set; }

    /// <summary>业务单据 ID（可选）</summary>
    public string? BizId { get; private set; }

    /// <summary>来源渠道</summary>
    public NotificationChannel Channel { get; private set; }

    /// <summary>是否已读</summary>
    public bool IsRead { get; private set; }

    /// <summary>已读时间</summary>
    public DateTime? ReadAt { get; private set; }

    /// <summary>标记已读（幂等：重复调用不更新 ReadAt）</summary>
    /// <param name="readAt">已读时间</param>
    public void MarkRead(DateTime readAt)
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = readAt;
        UpdatedAt = readAt;
    }

    /// <summary>软删除（移除出收件箱，保留审计数据）</summary>
    public void Delete() => IsDeleted = true;

    private static string ValidateTitle(string title)
    {
        var trimmed = (title ?? string.Empty).Trim();
        if (trimmed.Length is < 1 or > 200)
            throw new DomainException("通知标题长度需在 1-200 字符之间", "INVALID_NOTIFICATION_TITLE");
        return trimmed;
    }

    private static string ValidateContent(string content)
    {
        var trimmed = (content ?? string.Empty).Trim();
        if (trimmed.Length is < 1 or > 2000)
            throw new DomainException("通知内容长度需在 1-2000 字符之间", "INVALID_NOTIFICATION_CONTENT");
        return trimmed;
    }
}

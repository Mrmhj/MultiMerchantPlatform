using BuildingBlocks.Core.Exceptions;
using NotificationService.Application.Hubs;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.DTOs;
using NotificationService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NotificationService.Application.Services;

/// <summary>
/// 站内信发送器 — 模板渲染（可选）→ 落库 → SignalR 实时推送（可选）。
/// 用户收件箱核心写入通道，内部接口与其他业务服务共用。
/// </summary>
public sealed class NotificationSender(
    NotificationDbContext db,
    NotificationDispatcher dispatcher)
{
    /// <summary>发送站内信</summary>
    /// <param name="request">发送请求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>发送结果（含通知 ID 与实时送达标记）</returns>
    public async Task<SendInAppNotificationResponse> SendAsync(
        SendInAppNotificationRequest request, CancellationToken ct = default)
    {
        var (title, content) = await ResolveContentAsync(request, ct);

        var notification = new Notification(
            request.UserId, request.MerchantId, request.Type, title, content,
            request.BizType, request.BizId, NotificationChannel.InApp);

        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);

        var delivered = false;
        if (request.Realtime)
        {
            await dispatcher.PushAsync(notification.UserId, NotificationMapper.ToResponse(notification));
            delivered = true; // SignalR 定向推送无在线连接时静默丢弃，语义等价已送达
        }

        return new SendInAppNotificationResponse
        {
            NotificationId = notification.Id,
            RealtimeDelivered = delivered,
        };
    }

    /// <summary>解析标题与内容（指定模板则渲染，否则直接用传入值）</summary>
    private async Task<(string Title, string Content)> ResolveContentAsync(
        SendInAppNotificationRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.TemplateCode))
        {
            var template = await db.Templates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Code == request.TemplateCode.Trim() && t.IsActive, ct)
                ?? throw new DomainException($"通知模板 {request.TemplateCode} 不存在或未启用", "TEMPLATE_NOT_FOUND");

            var data = request.TemplateData ?? new Dictionary<string, object?>();
            return (template.RenderTitle(data), template.RenderBody(data));
        }

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
            throw new DomainException("未指定模板时，标题与内容均不能为空", "INVALID_NOTIFICATION_CONTENT");

        return (request.Title, request.Content);
    }
}

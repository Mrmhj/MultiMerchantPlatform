using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.DTOs;
using NotificationService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NotificationService.Application.Commands;

/// <summary>发送站内信命令（内部接口，X-Internal-Key）</summary>
/// <param name="Request">发送请求</param>
public sealed record SendInAppNotificationCommand(SendInAppNotificationRequest Request)
    : ICommand<SendInAppNotificationResponse>;

/// <summary>发送站内信命令处理器</summary>
public sealed class SendInAppNotificationCommandHandler(
    NotificationSender sender) : ICommandHandler<SendInAppNotificationCommand, SendInAppNotificationResponse>
{
    /// <inheritdoc />
    public Task<SendInAppNotificationResponse> HandleAsync(
        SendInAppNotificationCommand command, CancellationToken ct = default)
        => sender.SendAsync(command.Request, ct);
}

/// <summary>发送短信命令（内部接口，X-Internal-Key）</summary>
/// <param name="Request">发送请求</param>
public sealed record SendSmsCommand(SendSmsRequest Request) : ICommand<SendSmsResponse>;

/// <summary>发送短信命令处理器</summary>
public sealed class SendSmsCommandHandler(
    NotificationDbContext db,
    SmsSender sender) : ICommandHandler<SendSmsCommand, SendSmsResponse>
{
    /// <inheritdoc />
    public async Task<SendSmsResponse> HandleAsync(SendSmsCommand command, CancellationToken ct = default)
    {
        var r = command.Request;
        var sms = new SmsMessage(r.Phone, r.Content, r.MaxRetryCount);
        db.SmsMessages.Add(sms);
        await db.SaveChangesAsync(ct);

        try
        {
            await sender.SendAsync(sms, ct);
        }
        catch (Exception ex)
        {
            sms.MarkFailed(ex.Message, DateTime.UtcNow);
        }

        await db.SaveChangesAsync(ct);
        return new SendSmsResponse { SmsId = sms.Id, Status = sms.Status, DryRun = sender.IsDryRun };
    }
}

/// <summary>发送 Push 命令（内部接口，X-Internal-Key）</summary>
/// <param name="Request">发送请求</param>
public sealed record SendPushCommand(SendPushRequest Request) : ICommand<SendPushResponse>;

/// <summary>发送 Push 命令处理器</summary>
public sealed class SendPushCommandHandler(
    NotificationDbContext db,
    PushSender sender) : ICommandHandler<SendPushCommand, SendPushResponse>
{
    /// <inheritdoc />
    public async Task<SendPushResponse> HandleAsync(SendPushCommand command, CancellationToken ct = default)
    {
        var r = command.Request;
        var push = new PushMessage(r.DeviceToken, r.Title, r.Content, r.MaxRetryCount);
        db.PushMessages.Add(push);
        await db.SaveChangesAsync(ct);

        try
        {
            await sender.SendAsync(push, ct);
        }
        catch (Exception ex)
        {
            push.MarkFailed(ex.Message, DateTime.UtcNow);
        }

        await db.SaveChangesAsync(ct);
        return new SendPushResponse { PushId = push.Id, Status = push.Status, DryRun = sender.IsDryRun };
    }
}

/// <summary>标记单条通知已读命令（用户端）</summary>
/// <param name="UserId">当前用户 ID</param>
/// <param name="NotificationId">通知 ID</param>
public sealed record MarkNotificationReadCommand(Guid UserId, Guid NotificationId) : ICommand<NotificationResponse>;

/// <summary>标记单条通知已读命令处理器（校验归属：仅可操作自己的通知）</summary>
public sealed class MarkNotificationReadCommandHandler(
    NotificationDbContext db,
    TimeProvider timeProvider) : ICommandHandler<MarkNotificationReadCommand, NotificationResponse>
{
    /// <inheritdoc />
    public async Task<NotificationResponse> HandleAsync(
        MarkNotificationReadCommand command, CancellationToken ct = default)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == command.NotificationId && n.UserId == command.UserId
                && !n.IsDeleted, ct)
            ?? throw new NotFoundException("通知", command.NotificationId);

        notification.MarkRead(timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return NotificationMapper.ToResponse(notification);
    }
}

/// <summary>全部标记已读命令（用户端）</summary>
/// <param name="UserId">当前用户 ID</param>
public sealed record MarkAllNotificationsReadCommand(Guid UserId) : ICommand<UnreadCountResponse>;

/// <summary>全部标记已读命令处理器（返回剩余未读数 0）</summary>
public sealed class MarkAllNotificationsReadCommandHandler(
    NotificationDbContext db,
    TimeProvider timeProvider) : ICommandHandler<MarkAllNotificationsReadCommand, UnreadCountResponse>
{
    /// <inheritdoc />
    public async Task<UnreadCountResponse> HandleAsync(
        MarkAllNotificationsReadCommand command, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var unread = await db.Notifications
            .Where(n => n.UserId == command.UserId && !n.IsRead && !n.IsDeleted)
            .ToListAsync(ct);

        foreach (var n in unread)
        {
            n.MarkRead(now);
        }

        await db.SaveChangesAsync(ct);
        return new UnreadCountResponse { UnreadCount = 0 };
    }
}

/// <summary>删除通知命令（用户端，软删除）</summary>
/// <param name="UserId">当前用户 ID</param>
/// <param name="NotificationId">通知 ID</param>
public sealed record DeleteNotificationCommand(Guid UserId, Guid NotificationId) : ICommand;

/// <summary>删除通知命令处理器（校验归属：仅可删除自己的通知）</summary>
public sealed class DeleteNotificationCommandHandler(
    NotificationDbContext db) : ICommandHandler<DeleteNotificationCommand>
{
    /// <inheritdoc />
    public async Task<Unit> HandleAsync(DeleteNotificationCommand command, CancellationToken ct = default)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == command.NotificationId && n.UserId == command.UserId
                && !n.IsDeleted, ct)
            ?? throw new NotFoundException("通知", command.NotificationId);

        notification.Delete();
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}

/// <summary>创建通知模板命令（管理端，admin）</summary>
/// <param name="Request">模板配置</param>
public sealed record CreateNotificationTemplateCommand(SaveNotificationTemplateRequest Request)
    : ICommand<NotificationTemplateResponse>;

/// <summary>创建通知模板命令处理器</summary>
public sealed class CreateNotificationTemplateCommandHandler(
    NotificationDbContext db) : ICommandHandler<CreateNotificationTemplateCommand, NotificationTemplateResponse>
{
    /// <inheritdoc />
    public async Task<NotificationTemplateResponse> HandleAsync(
        CreateNotificationTemplateCommand command, CancellationToken ct = default)
    {
        var r = command.Request;
        var template = new NotificationTemplate(
            r.Code, r.TitleTemplate, r.BodyTemplate, r.Channels, r.Description);
        db.Templates.Add(template);
        await db.SaveChangesAsync(ct);
        return NotificationMapper.ToTemplateResponse(template);
    }
}

/// <summary>更新通知模板命令（管理端，admin）</summary>
/// <param name="TemplateId">模板 ID</param>
/// <param name="Request">模板配置</param>
public sealed record UpdateNotificationTemplateCommand(Guid TemplateId, SaveNotificationTemplateRequest Request)
    : ICommand<NotificationTemplateResponse>;

/// <summary>更新通知模板命令处理器</summary>
public sealed class UpdateNotificationTemplateCommandHandler(
    NotificationDbContext db) : ICommandHandler<UpdateNotificationTemplateCommand, NotificationTemplateResponse>
{
    /// <inheritdoc />
    public async Task<NotificationTemplateResponse> HandleAsync(
        UpdateNotificationTemplateCommand command, CancellationToken ct = default)
    {
        var template = await db.Templates.FirstOrDefaultAsync(t => t.Id == command.TemplateId, ct)
            ?? throw new NotFoundException("通知模板", command.TemplateId);

        var r = command.Request;
        template.Update(r.Code, r.TitleTemplate, r.BodyTemplate, r.Channels, r.Description);
        await db.SaveChangesAsync(ct);
        return NotificationMapper.ToTemplateResponse(template);
    }
}

/// <summary>启用/停用通知模板命令（管理端，admin）</summary>
/// <param name="TemplateId">模板 ID</param>
/// <param name="Enabled">是否启用</param>
public sealed record SetNotificationTemplateEnabledCommand(Guid TemplateId, bool Enabled)
    : ICommand<NotificationTemplateResponse>;

/// <summary>启用/停用通知模板命令处理器</summary>
public sealed class SetNotificationTemplateEnabledCommandHandler(
    NotificationDbContext db) : ICommandHandler<SetNotificationTemplateEnabledCommand, NotificationTemplateResponse>
{
    /// <inheritdoc />
    public async Task<NotificationTemplateResponse> HandleAsync(
        SetNotificationTemplateEnabledCommand command, CancellationToken ct = default)
    {
        var template = await db.Templates.FirstOrDefaultAsync(t => t.Id == command.TemplateId, ct)
            ?? throw new NotFoundException("通知模板", command.TemplateId);

        if (command.Enabled) template.Enable(); else template.Disable();
        await db.SaveChangesAsync(ct);
        return NotificationMapper.ToTemplateResponse(template);
    }
}

/// <summary>删除通知模板命令（管理端，admin）</summary>
/// <param name="TemplateId">模板 ID</param>
public sealed record DeleteNotificationTemplateCommand(Guid TemplateId) : ICommand;

/// <summary>删除通知模板命令处理器</summary>
public sealed class DeleteNotificationTemplateCommandHandler(
    NotificationDbContext db) : ICommandHandler<DeleteNotificationTemplateCommand>
{
    /// <inheritdoc />
    public async Task<Unit> HandleAsync(DeleteNotificationTemplateCommand command, CancellationToken ct = default)
    {
        var template = await db.Templates.FirstOrDefaultAsync(t => t.Id == command.TemplateId, ct)
            ?? throw new NotFoundException("通知模板", command.TemplateId);
        db.Templates.Remove(template);
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}

using EmailService.Application.Options;
using EmailService.Domain.Entities;
using EmailService.DTOs;
using EmailService.Infrastructure.Mail;
using EmailService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EmailService.Application;

/// <summary>
/// 邮件发送服务 — 模板渲染 + 落库 + 立即发送（失败进入后台重试队列）。
/// </summary>
public sealed class EmailSender(
    EmailDbContext db,
    ISmtpSender smtpSender,
    EmailTemplateRenderer renderer,
    TimeProvider timeProvider,
    IOptions<EmailOptions> options)
{
    private readonly EmailOptions _options = options.Value;

    /// <summary>发送单封邮件（支持模板渲染）</summary>
    public async Task<EmailMessage> SendAsync(SendEmailRequest request, CancellationToken ct = default)
    {
        var (subject, body) = await ResolveContentAsync(request, ct);

        var email = new EmailMessage(
            _options.DefaultFrom,
            request.To.Trim(),
            subject,
            body,
            request.IsHtml ?? true,
            request.Cc,
            request.Bcc,
            request.TemplateName,
            request.MaxRetryCount ?? 3);

        db.Emails.Add(email);
        await db.SaveChangesAsync(ct);

        await TrySendAsync(email, ct);

        await db.SaveChangesAsync(ct);
        return email;
    }

    /// <summary>批量发送（逐条发送，单条失败不影响其他）</summary>
    public async Task<IReadOnlyList<EmailMessage>> SendBatchAsync(
        IEnumerable<SendEmailRequest> requests, CancellationToken ct = default)
    {
        var results = new List<EmailMessage>();
        foreach (var request in requests)
        {
            if (ct.IsCancellationRequested)
                break;

            var (subject, body) = await ResolveContentAsync(request, ct);
            var email = new EmailMessage(
                _options.DefaultFrom, request.To.Trim(), subject, body,
                request.IsHtml ?? true, request.Cc, request.Bcc, request.TemplateName,
                request.MaxRetryCount ?? 3);
            db.Emails.Add(email);
            results.Add(email);
        }

        await db.SaveChangesAsync(ct);

        foreach (var email in results)
        {
            await TrySendAsync(email, ct);
        }

        await db.SaveChangesAsync(ct);
        return results;
    }

    /// <summary>手动重试（重置死信/失败邮件）</summary>
    public async Task<EmailMessage?> RetryAsync(Guid id, CancellationToken ct = default)
    {
        var email = await db.Emails.FindAsync([id], ct);
        if (email is null)
            return null;

        email.ResetForRetry(timeProvider);
        await TrySendAsync(email, ct);
        await db.SaveChangesAsync(ct);
        return email;
    }

    private async Task<(string Subject, string Body)> ResolveContentAsync(SendEmailRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.TemplateName))
        {
            var template = await db.Templates
                .FirstOrDefaultAsync(t => t.Name == request.TemplateName && t.IsActive, ct)
                ?? throw new KeyNotFoundException($"模板 {request.TemplateName} 不存在或未启用");

            return await renderer.RenderAsync(template, request.TemplateData ?? new Dictionary<string, object?>());
        }

        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new ArgumentException("主题（Subject）不能为空（未指定模板时必填）");
        if (string.IsNullOrWhiteSpace(request.Body))
            throw new ArgumentException("正文（Body）不能为空（未指定模板时必填）");

        return (request.Subject!, request.Body!);
    }

    private async Task TrySendAsync(EmailMessage email, CancellationToken ct)
    {
        try
        {
            await smtpSender.SendAsync(email, ct);
            email.MarkSent(timeProvider);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 取消请求时不标记失败，保持 Pending 由后台重试
        }
        catch (Exception ex)
        {
            email.MarkFailed(ex.Message, timeProvider,
                TimeSpan.FromSeconds(_options.RetryBaseIntervalSeconds),
                _options.MaxRetryDelaySeconds);
        }
    }
}

using EmailService.Application.Options;
using EmailService.Domain.Entities;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace EmailService.Infrastructure.Mail;

    /// <summary>SMTP 发送器接口（Strategy 模式 — 可替换为其他发送渠道）。</summary>
    public interface ISmtpSender
    {
        /// <summary>发送一封邮件（DryRun 模式下仅记录日志，不真实投递）</summary>
        /// <param name="email">待发送邮件实体</param>
        /// <param name="ct">取消令牌</param>
        Task SendAsync(EmailMessage email, CancellationToken ct = default);
    }

/// <summary>
/// MailKit SMTP 发送器。
/// DryRun 模式下不真实发送（本地无 SMTP 时开发用），仅记录日志。
/// </summary>
public sealed class SmtpSender(
    IOptions<EmailOptions> options,
    ILogger<SmtpSender> logger) : ISmtpSender
{
    private readonly EmailOptions _options = options.Value;

    /// <inheritdoc />
    public async Task SendAsync(EmailMessage email, CancellationToken ct = default)
    {
        if (_options.DryRun)
        {
            logger.LogInformation("[DryRun] 模拟发送邮件 To={To} Subject={Subject} IsHtml={IsHtml}",
                email.To, email.Subject, email.IsHtml);
            return;
        }

        using var client = new SmtpClient();
        await client.ConnectAsync(_options.Host, _options.Port,
            _options.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable, ct);

        if (!string.IsNullOrEmpty(_options.Username))
            await client.AuthenticateAsync(_options.Username, _options.Password, ct);

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(email.From));
        foreach (var addr in SplitAddresses(email.To))
            message.To.Add(MailboxAddress.Parse(addr));
        if (email.Cc is not null)
            foreach (var addr in SplitAddresses(email.Cc))
                message.Cc.Add(MailboxAddress.Parse(addr));
        if (email.Bcc is not null)
            foreach (var addr in SplitAddresses(email.Bcc))
                message.Bcc.Add(MailboxAddress.Parse(addr));

        message.Subject = email.Subject;

        var body = new BodyBuilder();
        if (email.IsHtml)
            body.HtmlBody = email.Body;
        else
            body.TextBody = email.Body;
        message.Body = body.ToMessageBody();

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    private static IEnumerable<string> SplitAddresses(string value) =>
        value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

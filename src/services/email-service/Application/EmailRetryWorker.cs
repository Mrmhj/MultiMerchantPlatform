using EmailService.Application.Options;
using EmailService.Domain.Enums;
using EmailService.Infrastructure.Mail;
using EmailService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EmailService.Application;

/// <summary>
/// 邮件后台重试 Worker — 轮询失败/待发邮件，指数退避重试，超限转死信。
/// </summary>
public sealed class EmailRetryWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<EmailOptions> options,
    ILogger<EmailRetryWorker> logger) : BackgroundService
{
    private readonly EmailOptions _options = options.Value;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("邮件重试 Worker 已启动，轮询间隔 {Interval}s", _options.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "邮件重试批次执行异常");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EmailDbContext>();
        var smtpSender = scope.ServiceProvider.GetRequiredService<ISmtpSender>();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var due = await db.Emails
            .Where(e => (e.Status == EmailStatus.Pending || e.Status == EmailStatus.Failed)
                        && e.NextRetryTime <= now)
            .OrderBy(e => e.NextRetryTime)
            .Take(50)
            .ToListAsync(ct);

        if (due.Count == 0)
            return;

        logger.LogInformation("发现 {Count} 封待重发邮件", due.Count);

        foreach (var email in due)
        {
            try
            {
                await smtpSender.SendAsync(email, ct);
                email.MarkSent(timeProvider);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                email.MarkFailed(ex.Message, timeProvider,
                    TimeSpan.FromSeconds(_options.RetryBaseIntervalSeconds),
                    _options.MaxRetryDelaySeconds);

                if (email.Status == EmailStatus.DeadLetter)
                {
                    logger.LogWarning("邮件 {Id} 至 {To} 超过 {Max} 次失败，转入死信：{Error}",
                        email.Id, email.To, email.MaxRetryCount, ex.Message);
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}

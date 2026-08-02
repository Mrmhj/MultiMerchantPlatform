using EmailService.Application;
using EmailService.Application.Options;
using EmailService.Infrastructure.Mail;
using EmailService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// email-service 依赖注入注册。
/// </summary>
public static class EmailServiceDependencyInjection
{
    /// <summary>注册 email-service 全部服务（配置 / 数据库 / SMTP / 发送 / 重试）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddEmailService(this IServiceCollection services, IConfiguration configuration)
    {
        // 配置
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations();

        // 数据库
        var connectionString = configuration.GetConnectionString("EmailDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:EmailDb");
        services.AddDbContext<EmailDbContext>(o => o.UseSqlServer(connectionString));

        // 基础设施
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<EmailTemplateRenderer>();
        services.AddSingleton<ISmtpSender, SmtpSender>();

        // 应用服务
        services.AddScoped<EmailSender>();

        // 后台重试 Worker
        services.AddHostedService<EmailRetryWorker>();

        return services;
    }
}

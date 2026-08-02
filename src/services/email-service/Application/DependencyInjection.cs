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

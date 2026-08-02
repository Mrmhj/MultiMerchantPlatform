using BuildingBlocks.Messaging;
using MessagingService.Application;
using MessagingService.Application.Options;
using MessagingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// messaging-service 依赖注入注册。
/// </summary>
public static class MessagingServiceDependencyInjection
{
    /// <summary>注册 messaging-service 全部服务（配置 / 数据库 / 发布器 / 订阅管理 / 后台分发器）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddMessagingService(this IServiceCollection services, IConfiguration configuration)
    {
        // 配置
        services.AddOptions<MessagingOptions>()
            .Bind(configuration.GetSection(MessagingOptions.SectionName))
            .ValidateDataAnnotations();

        // 数据库
        var connectionString = configuration.GetConnectionString("MessagingDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:MessagingDb");
        services.AddDbContext<MessagingDbContext>(o => o.UseSqlServer(connectionString));

        // HTTP 客户端（分发器投递用）
        services.AddHttpClient("MessagingDispatcher");

        // 应用服务
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<MessagePublisher>();
        services.AddScoped<IMessagePublisher>(sp => sp.GetRequiredService<MessagePublisher>());
        services.AddScoped<SubscriptionManager>();

        // 后台分发器
        services.AddHostedService<MessageDispatcher>();

        return services;
    }
}

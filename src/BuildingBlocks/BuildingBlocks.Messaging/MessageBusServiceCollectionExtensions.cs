using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.Messaging;

/// <summary>
/// 消息总线依赖注入注册 — 支持两种传输策略：
/// - In-Memory：开发环境（进程内队列，无持久化）
/// - HTTP：生产默认（通过 messaging-service REST API，SQL Server 持久化）
/// </summary>
public static class MessageBusServiceCollectionExtensions
{
    /// <summary>注册 In-Memory 发布器（开发环境，无持久化）</summary>
    public static IServiceCollection AddInMemoryMessageBus(this IServiceCollection services)
        => services.AddSingleton<IMessagePublisher, InMemoryMessagePublisher>();

    /// <summary>注册 HTTP 发布器（连接 messaging-service，生产默认）</summary>
    public static IServiceCollection AddHttpMessageBus(
        this IServiceCollection services,
        Action<MessageBusOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<MessageBusOptions>()
                .BindConfiguration(MessageBusOptions.SectionName);
        }

        services.AddHttpClient<HttpMessagePublisher>();
        services.TryAddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<HttpMessagePublisher>());
        return services;
    }
}

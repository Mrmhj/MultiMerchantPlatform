using BuildingBlocks.Core.CQRS;
using BuildingBlocks.MultiTenant;
using BuildingBlocks.Security;
using ImService.Application.Commands;
using ImService.Application.Hubs;
using ImService.Infrastructure;
using ImService.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// im-service 依赖注入注册。
/// </summary>
public static class ImServiceDependencyInjection
{
    /// <summary>注册 im-service 全部服务（配置 / 数据库 / 多租户 / SignalR 组件 / CQRS 处理器）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddImService(this IServiceCollection services, IConfiguration configuration)
    {
        // 数据库
        var connectionString = configuration.GetConnectionString("ImDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:ImDb");
        services.AddDbContext<ImDbContext>(o => o.UseSqlServer(connectionString));

        // 多租户（X-Merchant-Id 请求头）与当前用户
        services.TryAddSingleton(TimeProvider.System);
        services.AddCurrentUser();
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantProvider, HttpMerchantProvider>();

        // SignalR：强类型客户端 + 连接管理 + 消息分发
        services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();
        services.AddSingleton<UserConnectionManager>();
        services.AddSingleton<MessageDispatcher>();

        // 中介者 + CQRS 处理器
        services.AddMediator();
        services.AddCqrsHandlers(typeof(GetOrCreatePrivateSessionCommandHandler).Assembly);

        return services;
    }
}

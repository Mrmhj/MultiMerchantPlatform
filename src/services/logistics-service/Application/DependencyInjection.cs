using BuildingBlocks.Core.CQRS;
using BuildingBlocks.MultiTenant;
using BuildingBlocks.Security;
using LogisticsService.Application.Commands;
using LogisticsService.Application.Queries;
using LogisticsService.Infrastructure;
using LogisticsService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// logistics-service 依赖注入注册。
/// </summary>
public static class LogisticsServiceDependencyInjection
{
    /// <summary>注册 logistics-service 全部服务（配置 / 数据库 / 多租户 / CQRS 处理器）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddLogisticsService(this IServiceCollection services, IConfiguration configuration)
    {
        // 数据库
        var connectionString = configuration.GetConnectionString("LogisticsDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:LogisticsDb");
        services.AddDbContext<LogisticsDbContext>(o => o.UseSqlServer(connectionString));

        // 多租户（X-Merchant-Id 请求头）与当前用户
        services.TryAddSingleton(TimeProvider.System);
        services.AddCurrentUser();
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantProvider, HttpMerchantProvider>();

        // 中介者 + CQRS 处理器
        services.AddMediator();
        services.AddCqrsHandlers(typeof(CreateShipmentCommandHandler).Assembly);

        return services;
    }
}

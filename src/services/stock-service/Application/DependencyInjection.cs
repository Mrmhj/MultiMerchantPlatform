using BuildingBlocks.Core.CQRS;
using BuildingBlocks.MultiTenant;
using BuildingBlocks.Security;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockService.Application.Commands;
using StockService.Application.Queries;
using StockService.Infrastructure;
using StockService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// stock-service 依赖注入注册。
/// </summary>
public static class StockServiceDependencyInjection
{
    /// <summary>注册 stock-service 全部服务（数据库 / 多租户 / CQRS 处理器）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddStockService(this IServiceCollection services, IConfiguration configuration)
    {
        // 数据库
        var connectionString = configuration.GetConnectionString("StockDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:StockDb");
        services.AddDbContext<StockDbContext>(o => o.UseSqlServer(connectionString));

        // 当前用户与商户
        services.TryAddSingleton(TimeProvider.System);
        services.AddCurrentUser();
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantProvider, HttpMerchantProvider>();

        // 中介者 + CQRS 处理器
        services.AddMediator();
        services.AddCqrsHandlers(typeof(CreateStockCommandHandler).Assembly);

        return services;
    }
}

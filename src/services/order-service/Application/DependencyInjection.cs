using BuildingBlocks.Core.CQRS;
using BuildingBlocks.MultiTenant;
using BuildingBlocks.Security;
using OrderService.Application.Commands;
using OrderService.Application.Queries;
using OrderService.Infrastructure;
using OrderService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// order-service 依赖注入注册。
/// </summary>
public static class OrderServiceDependencyInjection
{
    /// <summary>注册 order-service 全部服务（配置 / 数据库 / 当前用户与商户 / CQRS 处理器）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddOrderService(this IServiceCollection services, IConfiguration configuration)
    {
        // 数据库
        var connectionString = configuration.GetConnectionString("OrderDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:OrderDb");
        services.AddDbContext<OrderDbContext>(o => o.UseSqlServer(connectionString));

        // 当前用户（JWT）与商户（X-Merchant-Id）
        services.TryAddSingleton(TimeProvider.System);
        services.AddCurrentUser();
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantProvider, HttpMerchantProvider>();

        // 中介者 + CQRS 处理器
        services.AddMediator();
        services.AddCqrsHandlers(typeof(CreateOrderCommandHandler).Assembly);

        return services;
    }
}

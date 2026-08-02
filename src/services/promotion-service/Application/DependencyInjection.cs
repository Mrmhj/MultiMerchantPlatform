using BuildingBlocks.Core.CQRS;
using BuildingBlocks.MultiTenant;
using BuildingBlocks.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PromotionService.Application.Commands;
using PromotionService.Application.Queries;
using PromotionService.Infrastructure;
using PromotionService.Infrastructure.Persistence;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// promotion-service 依赖注入注册。
/// </summary>
public static class PromotionServiceDependencyInjection
{
    /// <summary>注册 promotion-service 全部服务（配置 / 数据库 / 多租户 / CQRS 处理器）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddPromotionService(this IServiceCollection services, IConfiguration configuration)
    {
        // 数据库
        var connectionString = configuration.GetConnectionString("PromotionDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:PromotionDb");
        services.AddDbContext<PromotionDbContext>(o => o.UseSqlServer(connectionString));

        // 多租户（X-Merchant-Id 请求头）与当前用户
        services.TryAddSingleton(TimeProvider.System);
        services.AddCurrentUser();
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantProvider, HttpMerchantProvider>();

        // 中介者 + CQRS 处理器
        services.AddMediator();
        services.AddCqrsHandlers(typeof(CreateCouponCommandHandler).Assembly);

        return services;
    }
}

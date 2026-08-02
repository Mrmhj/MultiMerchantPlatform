using BuildingBlocks.Core.CQRS;
using BuildingBlocks.MultiTenant;
using BuildingBlocks.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReviewService.Application.Commands;
using ReviewService.Application.Queries;
using ReviewService.Infrastructure;
using ReviewService.Infrastructure.Persistence;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// review-service 依赖注入注册。
/// </summary>
public static class ReviewServiceDependencyInjection
{
    /// <summary>注册 review-service 全部服务（配置 / 数据库 / 多租户 / CQRS 处理器）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddReviewService(this IServiceCollection services, IConfiguration configuration)
    {
        // 数据库
        var connectionString = configuration.GetConnectionString("ReviewDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:ReviewDb");
        services.AddDbContext<ReviewDbContext>(o => o.UseSqlServer(connectionString));

        // 多租户（X-Merchant-Id 请求头）与当前用户
        services.TryAddSingleton(TimeProvider.System);
        services.AddCurrentUser();
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantProvider, HttpMerchantProvider>();

        // 中介者 + CQRS 处理器
        services.AddMediator();
        services.AddCqrsHandlers(typeof(CreateReviewCommandHandler).Assembly);

        return services;
    }
}

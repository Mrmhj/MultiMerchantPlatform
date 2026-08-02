using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Security;
using MerchantService.Application;
using MerchantService.Application.Commands;
using MerchantService.Application.Queries;
using MerchantService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// merchant-service 依赖注入注册。
/// </summary>
public static class MerchantServiceDependencyInjection
{
    /// <summary>注册 merchant-service 全部服务（配置 / 数据库 / CQRS 处理器 / 当前用户）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddMerchantService(this IServiceCollection services, IConfiguration configuration)
    {
        // 数据库
        var connectionString = configuration.GetConnectionString("MerchantDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:MerchantDb");
        services.AddDbContext<MerchantDbContext>(o => o.UseSqlServer(connectionString));

        // 时间与当前用户
        services.TryAddSingleton(TimeProvider.System);
        services.AddCurrentUser();

        // 中介者 + CQRS 处理器（按程序集扫描注册）
        services.AddMediator();
        services.AddCqrsHandlers(typeof(ApplyMerchantCommandHandler).Assembly);

        return services;
    }
}

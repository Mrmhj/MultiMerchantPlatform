using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Security;
using SearchService.Application.Commands;
using SearchService.Application.Queries;
using SearchService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// search-service 依赖注入注册。
/// </summary>
public static class SearchServiceDependencyInjection
{
    /// <summary>注册 search-service 全部服务（配置 / 数据库 / 当前用户 / CQRS 处理器）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddSearchService(this IServiceCollection services, IConfiguration configuration)
    {
        // 数据库
        var connectionString = configuration.GetConnectionString("SearchDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:SearchDb");
        services.AddDbContext<SearchDbContext>(o => o.UseSqlServer(connectionString));

        // 当前用户（JWT）
        services.TryAddSingleton(TimeProvider.System);
        services.AddCurrentUser();
        services.AddHttpContextAccessor();

        // 中介者 + CQRS 处理器
        services.AddMediator();
        services.AddCqrsHandlers(typeof(UpsertSearchIndexCommandHandler).Assembly);

        return services;
    }
}

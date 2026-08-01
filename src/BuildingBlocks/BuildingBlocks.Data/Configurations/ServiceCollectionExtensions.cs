using BuildingBlocks.Data.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Data.Configurations;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加数据层 — 支持 EF Core / SqlSugar / Dapper 切换。
    /// </summary>
    public static IServiceCollection AddDataLayer(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "Data",
        Action<DataOptions>? configure = null)
    {
        var options = new DataOptions();
        configuration.GetSection(sectionName).Bind(options);
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.Configure<DataOptions>(configuration.GetSection(sectionName));

        // 注册连接切换器
        services.AddSingleton<Abstractions.IDbConnectionSwitcher, Implementations.DbConnectionSwitcher>();

        // 按 ORM 类型注册仓储
        switch (options.DefaultOrm)
        {
            case OrmType.EfCore:
                // 各服务自行注册 DbContext，这里只注册仓储工厂
                services.AddScoped(typeof(Abstractions.IRepository<>), typeof(Implementations.EfRepository<>));
                break;

            case OrmType.SqlSugar:
                // SqlSugar 仓储由各服务模块注册时配置
                // services.AddScoped(typeof(Abstractions.IRepository<>), typeof(Implementations.SqlSugarRepository<>));
                break;

            case OrmType.Dapper:
                // Dapper 仓储由各服务模块注册时配置
                // services.AddScoped(typeof(Abstractions.IRepository<>), typeof(Implementations.DapperRepository<>));
                break;
        }

        return services;
    }

    /// <summary>
    /// 注册 EF Core DbContext（各微服务调用）。
    /// </summary>
    public static IServiceCollection AddEfCoreDbContext<TContext>(
        this IServiceCollection services,
        string connectionString,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>(options =>
        {
            options.UseSqlServer(connectionString);
            options.EnableSensitiveDataLogging(false);
        }, lifetime);

        return services;
    }
}

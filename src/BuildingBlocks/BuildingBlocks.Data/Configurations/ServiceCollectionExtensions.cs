using BuildingBlocks.Data.Abstractions;
using BuildingBlocks.Data.Implementations;
using BuildingBlocks.Data.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace BuildingBlocks.Data.Configurations;

/// <summary>
/// 数据层 DI 注册扩展 — 支持 EF Core / SqlSugar / Dapper 切换（Strategy 模式）。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加数据层 — 按 OrmType 配置自动选择仓储策略。
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

        // 注册连接切换器（Factory 模式）
        services.AddSingleton<IDbConnectionSwitcher, DbConnectionSwitcher>();

        // 注册 TimeProvider（用于可测试的时间依赖）
        services.AddSingleton<TimeProvider>(TimeProvider.System);

        // 按 ORM 类型注册仓储策略
        switch (options.DefaultOrm)
        {
            case OrmType.EfCore:
                services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
                services.AddScoped(typeof(ISpecificationRepository<>), typeof(EfSpecificationRepository<>));
                break;

            case OrmType.SqlSugar:
                // SqlSugar 仓储由各服务模块注册时配置
                // services.AddScoped(typeof(IRepository<>), typeof(SqlSugarRepository<>));
                break;

            case OrmType.Dapper:
                // Dapper 仓储由各服务模块注册时配置
                // services.AddScoped(typeof(IRepository<>), typeof(DapperRepository<>));
                break;
        }

        return services;
    }

    /// <summary>
    /// 注册 EF Core DbContext + 工作单元（各微服务调用）。
    /// </summary>
    public static IServiceCollection AddEfCore<TContext>(
        this IServiceCollection services,
        string connectionString,
        Action<DbContextOptionsBuilder>? configure = null)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>(options =>
        {
            options.UseSqlServer(connectionString);
            options.EnableSensitiveDataLogging(false);
            configure?.Invoke(options);
        });

        // 注册工作单元
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        return services;
    }
}

using BuildingBlocks.Data.Abstractions;
using BuildingBlocks.Data.Implementations;
using BuildingBlocks.Data.Options;
using BuildingBlocks.Data.Strategies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SqlSugar;

namespace BuildingBlocks.Data.Configurations;

/// <summary>
/// 数据层 DI 注册扩展 — 支持 EF Core / SqlSugar / Dapper 切换（Strategy + Factory 模式）。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加数据层 — 按 OrmType 配置自动选择仓储策略，并注册仓储工厂。
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

        // 注册连接切换器（Factory 模式 — 多数据库连接）
        services.AddSingleton<IDbConnectionSwitcher, DbConnectionSwitcher>();

        // 注册 TimeProvider（用于可测试的时间依赖）
        services.TryAddSingleton(TimeProvider.System);

        // 注册 ORM 策略标记（Strategy 模式）
        services.TryAddSingleton<IOrmStrategy, EfCoreOrmStrategy>();
        switch (options.DefaultOrm)
        {
            case OrmType.SqlSugar:
                services.Replace(ServiceDescriptor.Singleton<IOrmStrategy, SqlSugarOrmStrategy>());
                break;
            case OrmType.Dapper:
                services.Replace(ServiceDescriptor.Singleton<IOrmStrategy, DapperOrmStrategy>());
                break;
        }

        // 注册三种仓储实现（RepositoryFactory 按配置解析对应实现）
        services.TryAddScoped(typeof(EfRepository<>));
        services.TryAddScoped(typeof(EfSpecificationRepository<>));
        services.TryAddScoped(typeof(SqlSugarRepository<>));
        services.TryAddScoped(typeof(DapperRepository<>));

        // 统一接口绑定（按 DefaultOrm 选择实现）
        services.AddScoped(typeof(IRepository<>), ResolveRepositoryType(options.DefaultOrm));
        if (options.DefaultOrm == OrmType.EfCore)
        {
            services.AddScoped(typeof(ISpecificationRepository<>), typeof(EfSpecificationRepository<>));
        }

        // 注册仓储工厂（Factory 模式）
        services.AddScoped<IRepositoryFactory, RepositoryFactory>();

        // 按 ORM 类型注册基础设施
        switch (options.DefaultOrm)
        {
            case OrmType.SqlSugar:
                services.TryAddSingleton<ISqlSugarClient>(_ => CreateSqlSugarClient(options));
                break;
            case OrmType.Dapper:
                // Dapper 仓储已注册，依赖 IDbConnectionSwitcher（已注册）
                break;
            default:
                // EF Core：DbContext 由各服务通过 AddEfCore&lt;TContext&gt; 注册
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

    private static Type ResolveRepositoryType(OrmType orm) => orm switch
    {
        OrmType.SqlSugar => typeof(SqlSugarRepository<>),
        OrmType.Dapper => typeof(DapperRepository<>),
        _ => typeof(EfRepository<>),
    };

    private static ISqlSugarClient CreateSqlSugarClient(DataOptions options)
    {
        var connectionString = options.Connections.GetValueOrDefault(options.DefaultConnectionName)
            ?? throw new InvalidOperationException($"缺少连接字符串: Data:Connections:{options.DefaultConnectionName}");

        return new SqlSugarScope(new ConnectionConfig
        {
            ConnectionString = connectionString,
            DbType = DbType.SqlServer,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute,
        });
    }
}

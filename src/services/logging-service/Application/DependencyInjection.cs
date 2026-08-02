using LoggingService.Application;
using LoggingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// logging-service 依赖注入注册。
/// </summary>
public static class LoggingServiceDependencyInjection
{
    /// <summary>注册 logging-service 全部服务（配置 / 数据库 / 写入 / 查询 / 统计）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddLoggingService(this IServiceCollection services, IConfiguration configuration)
    {
        // 数据库
        var connectionString = configuration.GetConnectionString("LoggingDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:LoggingDb");
        services.AddDbContext<LoggingDbContext>(o => o.UseSqlServer(connectionString));

        // 应用服务
        services.AddScoped<LogIngestService>();
        services.AddScoped<LogQueryService>();
        services.AddScoped<LogStatsService>();

        return services;
    }
}

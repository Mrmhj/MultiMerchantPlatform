using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Security;
using PerformanceService.Application.Commands;
using PerformanceService.Application.Services;
using PerformanceService.Infrastructure;
using PerformanceService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// performance-service 依赖注入注册。
/// </summary>
public static class PerformanceServiceDependencyInjection
{
    /// <summary>注册 performance-service 全部服务（配置 / 数据库 / HttpClient / 压测引擎 / 监控采集 / CQRS 处理器）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddPerformanceService(this IServiceCollection services, IConfiguration configuration)
    {
        // 配置绑定
        services.Configure<MonitoringOptions>(configuration.GetSection("Monitoring"));
        services.Configure<ReportOptions>(configuration.GetSection("Reports"));
        services.Configure<LoadTestOptions>(configuration.GetSection("LoadTest"));

        // 数据库（MMP_Infra，与 messaging / logging 共用）
        var connectionString = configuration.GetConnectionString("PerformanceDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:PerformanceDb");
        services.AddDbContext<PerformanceDbContext>(o => o.UseSqlServer(connectionString));

        // 时间与当前用户
        services.TryAddSingleton(TimeProvider.System);
        services.AddCurrentUser();

        // HttpClient：monitor（监控探测）+ loadtest（压测发送）
        services.AddHttpClient("monitor", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHttpClient("loadtest", client =>
        {
            // 单请求超时 100s（目标服务响应极慢时兜底），压测总时长由引擎按 DurationSeconds 控制
            client.Timeout = TimeSpan.FromSeconds(100);
        });

        // 核心服务
        services.AddSingleton<ProcessMetricsProvider>();
        services.AddScoped<AlertEvaluator>();
        services.AddSingleton<HtmlReportGenerator>();
        services.AddSingleton<LoadTestEngine>();
        services.AddHostedService(sp => sp.GetRequiredService<LoadTestEngine>());
        services.AddSingleton<MetricsCollector>();
        services.AddHostedService(sp => sp.GetRequiredService<MetricsCollector>());

        // 中介者 + CQRS 处理器
        services.AddMediator();
        services.AddCqrsHandlers(typeof(CreateLoadTestTaskCommandHandler).Assembly);

        return services;
    }
}

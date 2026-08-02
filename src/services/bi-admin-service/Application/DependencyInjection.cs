using BiAdminService.Application.Queries;
using BiAdminService.Application.Services;
using BiAdminService.DTOs;
using BiAdminService.Infrastructure;
using BiAdminService.Infrastructure.Persistence;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// bi-admin-service 依赖注入注册。
/// </summary>
public static class BiAdminServiceDependencyInjection
{
    /// <summary>注册 bi-admin-service 全部服务（配置 / 数据库 / 内部取数客户端 / 同步 / CQRS 处理器）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddBiAdminService(this IServiceCollection services, IConfiguration configuration)
    {
        // 数据库（MMP_BI 独立库）
        var connectionString = configuration.GetConnectionString("BiDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:BiDb");
        services.AddDbContext<BiDbContext>(o => o.UseSqlServer(connectionString));

        services.TryAddSingleton(TimeProvider.System);
        services.AddCurrentUser();

        // 各服务内部取数客户端（命名 HttpClient 携带 X-Internal-Key 默认头，IHttpClientFactory 按名取）
        var internalKey = configuration["Internal:Key"] ?? string.Empty;
        services.AddHttpClient("order", client =>
        {
            client.BaseAddress = new Uri(configuration["Services:OrderService:BaseUrl"] ?? "http://localhost:8004");
            client.Timeout = TimeSpan.FromSeconds(30);
            if (!string.IsNullOrEmpty(internalKey))
                client.DefaultRequestHeaders.Add("X-Internal-Key", internalKey);
        });
        services.AddHttpClient("merchant", client =>
        {
            client.BaseAddress = new Uri(configuration["Services:MerchantService:BaseUrl"] ?? "http://localhost:8002");
            client.Timeout = TimeSpan.FromSeconds(30);
            if (!string.IsNullOrEmpty(internalKey))
                client.DefaultRequestHeaders.Add("X-Internal-Key", internalKey);
        });
        services.AddHttpClient("product", client =>
        {
            client.BaseAddress = new Uri(configuration["Services:ProductService:BaseUrl"] ?? "http://localhost:8003");
            client.Timeout = TimeSpan.FromSeconds(30);
            if (!string.IsNullOrEmpty(internalKey))
                client.DefaultRequestHeaders.Add("X-Internal-Key", internalKey);
        });
        services.AddHttpClient("identity", client =>
        {
            client.BaseAddress = new Uri(configuration["Services:IdentityService:BaseUrl"] ?? "http://localhost:8001");
            client.Timeout = TimeSpan.FromSeconds(30);
            if (!string.IsNullOrEmpty(internalKey))
                client.DefaultRequestHeaders.Add("X-Internal-Key", internalKey);
        });
        services.AddScoped<BiDataClients>();
        services.AddScoped<BiSyncService>();

        // 中介者 + CQRS 处理器
        services.AddMediator();
        services.AddCqrsHandlers(typeof(BiOverviewQueryHandler).Assembly);

        return services;
    }
}

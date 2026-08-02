using BuildingBlocks.Communication;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.MultiTenant;
using BuildingBlocks.Security;
using SettlementService.Application.Commands;
using SettlementService.Application.Queries;
using SettlementService.Infrastructure;
using SettlementService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// settlement-service 依赖注入注册。
/// </summary>
public static class SettlementServiceDependencyInjection
{
    /// <summary>注册 settlement-service 全部服务（配置 / 数据库 / 多租户 / 订单客户端 / CQRS 处理器）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddSettlementService(this IServiceCollection services, IConfiguration configuration)
    {
        // 数据库
        var connectionString = configuration.GetConnectionString("SettlementDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:SettlementDb");
        services.AddDbContext<SettlementDbContext>(o => o.UseSqlServer(connectionString));

        // 多租户（X-Merchant-Id 请求头）与当前用户
        services.TryAddSingleton(TimeProvider.System);
        services.AddCurrentUser();
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantProvider, HttpMerchantProvider>();

        // 订单服务客户端（命名 HttpClient 携带 X-Internal-Key 默认头，IHttpClientFactory 按名取）
        var orderBaseUrl = configuration["Services:OrderService:BaseUrl"] ?? "http://localhost:8004";
        var internalKey = configuration["Internal:Key"] ?? string.Empty;
        services.AddHttpClient("order", client =>
        {
            client.BaseAddress = new Uri(orderBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            if (!string.IsNullOrEmpty(internalKey))
                client.DefaultRequestHeaders.Add("X-Internal-Key", internalKey);
        });
        services.AddScoped<OrderServiceClient>();

        // 中介者 + CQRS 处理器
        services.AddMediator();
        services.AddCqrsHandlers(typeof(GenerateSettlementsCommandHandler).Assembly);

        return services;
    }
}

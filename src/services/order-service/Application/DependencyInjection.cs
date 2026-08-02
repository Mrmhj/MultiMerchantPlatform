using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Communication;
using BuildingBlocks.MultiTenant;
using BuildingBlocks.Security;
using OrderService.Application.Commands;
using OrderService.Application.Queries;
using OrderService.Infrastructure;
using OrderService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// order-service 依赖注入注册。
/// </summary>
public static class OrderServiceDependencyInjection
{
    /// <summary>注册 order-service 全部服务（配置 / 数据库 / 当前用户与商户 / 库存客户端 / CQRS 处理器）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddOrderService(this IServiceCollection services, IConfiguration configuration)
    {
        // 数据库
        var connectionString = configuration.GetConnectionString("OrderDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:OrderDb");
        services.AddDbContext<OrderDbContext>(o => o.UseSqlServer(connectionString));

        // 当前用户（JWT）与商户（X-Merchant-Id）
        services.TryAddSingleton(TimeProvider.System);
        services.AddCurrentUser();
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantProvider, HttpMerchantProvider>();

        // 库存服务客户端（命名 HttpClient 携带 X-Internal-Key 默认头）
        var stockBaseUrl = configuration["Services:StockService:BaseUrl"] ?? "http://localhost:8006";
        var internalKey = configuration["Internal:Key"] ?? string.Empty;
        services.AddHttpClient<IServiceClient, HttpServiceClient>("stock", client =>
        {
            client.BaseAddress = new Uri(stockBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            if (!string.IsNullOrEmpty(internalKey))
                client.DefaultRequestHeaders.Add("X-Internal-Key", internalKey);
        });
        services.AddScoped<StockServiceClient>();

        // 物流服务客户端（命名 HttpClient 携带 X-Internal-Key 默认头，IHttpClientFactory 按名取）
        var logisticsBaseUrl = configuration["Services:LogisticsService:BaseUrl"] ?? "http://localhost:8013";
        services.AddHttpClient("logistics", client =>
        {
            client.BaseAddress = new Uri(logisticsBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
            if (!string.IsNullOrEmpty(internalKey))
                client.DefaultRequestHeaders.Add("X-Internal-Key", internalKey);
        });
        services.AddScoped<LogisticsServiceClient>();

        // 中介者 + CQRS 处理器
        services.AddMediator();
        services.AddCqrsHandlers(typeof(CreateOrderCommandHandler).Assembly);

        return services;
    }
}

using BuildingBlocks.Communication;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Security;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PayService.Application.Commands;
using PayService.Application.Queries;
using PayService.Infrastructure;
using PayService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// pay-service 依赖注入注册。
/// </summary>
public static class PayServiceDependencyInjection
{
    /// <summary>注册 pay-service 全部服务（配置 / 数据库 / 服务客户端 / CQRS 处理器）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddPayService(this IServiceCollection services, IConfiguration configuration)
    {
        // 数据库
        var connectionString = configuration.GetConnectionString("PayDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:PayDb");
        services.AddDbContext<PayDbContext>(o => o.UseSqlServer(connectionString));

        // 当前用户
        services.TryAddSingleton(TimeProvider.System);
        services.AddCurrentUser();

        // 跨服务客户端：调用 order-service（命名 HttpClient 携带 X-Internal-Key 默认头）
        var orderBaseUrl = configuration["Services:OrderService:BaseUrl"]
            ?? "http://localhost:8004";
        var internalKey = configuration["Internal:Key"] ?? string.Empty;
        services.AddHttpClient<IServiceClient, HttpServiceClient>("order", client =>
        {
            client.BaseAddress = new Uri(orderBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            if (!string.IsNullOrEmpty(internalKey))
                client.DefaultRequestHeaders.Add("X-Internal-Key", internalKey);
        });
        services.AddScoped<OrderServiceClient>();

        // 中介者 + CQRS 处理器
        services.AddMediator();
        services.AddCqrsHandlers(typeof(CreatePaymentCommandHandler).Assembly);

        return services;
    }
}

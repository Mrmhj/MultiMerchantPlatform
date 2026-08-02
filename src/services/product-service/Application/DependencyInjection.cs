using BuildingBlocks.Cache;
using BuildingBlocks.Communication;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.MultiTenant;
using BuildingBlocks.Security;
using ProductService.Application.Commands;
using ProductService.Application.Queries;
using ProductService.Infrastructure;
using ProductService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// product-service 依赖注入注册。
/// </summary>
public static class ProductServiceDependencyInjection
{
    /// <summary>注册 product-service 全部服务（配置 / 数据库 / 多租户 / CQRS 处理器）</summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddProductService(this IServiceCollection services, IConfiguration configuration)
    {
        // 数据库
        var connectionString = configuration.GetConnectionString("ProductDb")
            ?? throw new InvalidOperationException("缺少连接字符串: ConnectionStrings:ProductDb");
        services.AddDbContext<ProductDbContext>(o => o.UseSqlServer(connectionString));

        // 缓存（Redis/In-Memory 切换，热数据缓存：C 端商品详情/列表）
        var useRedis = configuration.GetValue<bool>("Cache:UseRedis");
        var redisConnection = configuration.GetValue<string>("Cache:RedisConnection");
        services.AddCacheService(useRedis, redisConnection);

        // 服务间调用弹性配置（Polly 重试/熔断/超时，配置节 Resilience）
        services.AddServiceClientResilience(configuration);

        // 多租户（X-Merchant-Id 请求头）与当前用户
        services.TryAddSingleton(TimeProvider.System);
        services.AddCurrentUser();
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantProvider, HttpMerchantProvider>();

        // 中介者 + CQRS 处理器
        services.AddMediator();
        services.AddCqrsHandlers(typeof(CreateCategoryCommandHandler).Assembly);

        // 商户服务客户端（查询商户名，X-Internal-Key 默认头）
        var merchantBaseUrl = configuration["Services:MerchantService:BaseUrl"] ?? "http://localhost:8002";
        var searchBaseUrl = configuration["Services:SearchService:BaseUrl"] ?? "http://localhost:8008";
        var internalKey = configuration["Internal:Key"] ?? string.Empty;
        services.AddHttpClient<IServiceClient, HttpServiceClient>("merchant", client =>
        {
            client.BaseAddress = new Uri(merchantBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
            if (!string.IsNullOrEmpty(internalKey))
                client.DefaultRequestHeaders.Add("X-Internal-Key", internalKey);
        });
        services.AddHttpClient<IServiceClient, HttpServiceClient>("search", client =>
        {
            client.BaseAddress = new Uri(searchBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
            if (!string.IsNullOrEmpty(internalKey))
                client.DefaultRequestHeaders.Add("X-Internal-Key", internalKey);
        });
        services.AddScoped<MerchantServiceClient>();
        services.AddScoped<SearchServiceClient>();

        return services;
    }
}

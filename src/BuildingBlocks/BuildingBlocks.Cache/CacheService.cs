using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace BuildingBlocks.Cache;

/// <summary>
/// 缓存服务依赖注入注册 — 支持 In-Memory / Redis 切换（Strategy 模式）。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册缓存服务 — useRedis=true 时接入 Redis（StackExchange.Redis），
    /// Redis 不可用自动降级 In-Memory（方案 B 兜底，不阻塞启动）。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="useRedis">是否启用 Redis 缓存（默认 false 用 In-Memory）</param>
    /// <param name="redisConnectionString">Redis 完整连接串（StackExchange.Redis 格式，如 "localhost:6379,password=xxx"；无密码也可传 "localhost:6379"）；useRedis=true 时必填</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddCacheService(
        this IServiceCollection services,
        bool useRedis = false,
        string? redisConnectionString = null)
    {
        services.AddMemoryCache();

        if (useRedis && !string.IsNullOrWhiteSpace(redisConnectionString))
        {
            try
            {
                var options = ConfigurationOptions.Parse(redisConnectionString);
                // 兜底默认值（连接串未显式指定时生效）
                options.AbortOnConnectFail = true;
                options.ConnectTimeout = 3000;
                options.ConnectRetry = 1;
                var multiplexer = ConnectionMultiplexer.Connect(options);

                services.AddSingleton<IConnectionMultiplexer>(multiplexer);
                services.AddSingleton<ICacheService, RedisCacheService>();
                services.AddSingleton<IDistributedLock, RedisDistributedLock>();
            }
            catch (Exception)
            {
                // Redis 不可用 → 降级 In-Memory（方案 B 兜底）
                services.AddSingleton<ICacheService, InMemoryCacheService>();
                services.AddSingleton<IDistributedLock, InMemoryDistributedLock>();
            }
        }
        else
        {
            services.AddSingleton<ICacheService, InMemoryCacheService>();
            services.AddSingleton<IDistributedLock, InMemoryDistributedLock>();
        }

        return services;
    }
}

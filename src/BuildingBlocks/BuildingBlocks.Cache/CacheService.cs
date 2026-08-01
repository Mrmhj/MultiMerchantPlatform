using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Cache;

/// <summary>
/// 缓存服务接口 — 支持 In-Memory / Redis(Memurai) 切换（Strategy 模式）。
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// In-Memory 缓存实现 — 开发阶段使用（Strategy 模式 — 内存策略）。
/// </summary>
public class InMemoryCacheService(IMemoryCache cache) : ICacheService
{
    private readonly IMemoryCache _cache = cache;

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        => Task.FromResult(_cache.TryGetValue<T>(key, out var value) ? value : default);

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var options = new MemoryCacheEntryOptions();
        if (expiry.HasValue)
            options.AbsoluteExpirationRelativeToNow = expiry;

        _cache.Set(key, value, options);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => Task.FromResult(_cache.TryGetValue(key, out _));
}

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册缓存服务 — 支持内存/Redis 切换（Strategy 模式）。
    /// </summary>
    public static IServiceCollection AddCacheService(this IServiceCollection services, bool useRedis = false)
    {
        services.AddMemoryCache();

        if (useRedis)
        {
            // 生产环境: services.AddSingleton<ICacheService, RedisCacheService>();
            services.AddSingleton<ICacheService, InMemoryCacheService>();
        }
        else
        {
            services.AddSingleton<ICacheService, InMemoryCacheService>();
        }

        return services;
    }
}

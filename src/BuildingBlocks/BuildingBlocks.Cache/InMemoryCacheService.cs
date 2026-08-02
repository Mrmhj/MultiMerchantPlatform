using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace BuildingBlocks.Cache;

/// <summary>
/// In-Memory 缓存实现 — 开发阶段/Redis 不可用时兜底（Strategy 模式 — 内存策略）。
/// 原子操作基于进程内锁（单机部署语义正确；分布式场景需切 Redis 实现）。
/// </summary>
public sealed class InMemoryCacheService(IMemoryCache cache) : ICacheService
{
    private readonly IMemoryCache _cache = cache;
    private readonly object _lock = new();

    /// <summary>回源锁表（single-flight 防击穿，每键一个信号量）</summary>
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _backfillLocks = new();

    /// <summary>GetOrAddAsync 默认缓存 TTL（未显式指定时）</summary>
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromSeconds(60);

    /// <inheritdoc />
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        => Task.FromResult(_cache.TryGetValue<T>(key, out var value) ? value : default);

    /// <inheritdoc />
    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var options = new MemoryCacheEntryOptions();
        if (expiry.HasValue)
            options.AbsoluteExpirationRelativeToNow = expiry;

        _cache.Set(key, value, options);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => Task.FromResult(_cache.TryGetValue(key, out _));

    /// <inheritdoc />
    public Task<long> IncrementAsync(string key, long delta = 1, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var current = _cache.TryGetValue<long>(key, out var v) ? v : 0L;
            var next = current + delta;
            _cache.Set(key, next);
            return Task.FromResult(next);
        }
    }

    /// <inheritdoc />
    public Task<long> DecrementAsync(string key, long delta = 1, CancellationToken ct = default)
        => IncrementAsync(key, -delta, ct);

    /// <inheritdoc />
    public Task<bool> TryDeductAsync(string key, long delta, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var current = _cache.TryGetValue<long>(key, out var v) ? v : 0L;
            if (current < delta)
                return Task.FromResult(false);

            _cache.Set(key, current - delta);
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public async Task<T?> GetOrAddAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan? expiry = null,
        CancellationToken ct = default)
    {
        // 快路径：缓存命中直接返回
        if (_cache.TryGetValue(key, out T? cached))
            return cached;

        // 慢路径：per-key 信号量 single-flight（仅一个请求回源）
        var gate = _backfillLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            // 拿到锁后二次检查（double-check）：可能在等待期间已被写入
            if (_cache.TryGetValue(key, out T? rechecked))
                return rechecked;

            var result = await factory(ct);
            if (result is not null)
                await SetAsync(key, result, expiry ?? DefaultExpiry, ct);
            return result;
        }
        finally
        {
            gate.Release();
            // 清理空闲信号量（避免键表无限增长）
            if (gate.CurrentCount == 1)
                _backfillLocks.TryRemove(key, out _);
        }
    }
}

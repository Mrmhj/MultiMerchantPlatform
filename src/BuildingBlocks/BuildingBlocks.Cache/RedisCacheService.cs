using System.Text.Json;
using StackExchange.Redis;

namespace BuildingBlocks.Cache;

/// <summary>
/// Redis 缓存实现（Strategy 模式 — Redis 策略），基于 StackExchange.Redis。
/// 值以 JSON 字符串存储；计数操作用 Redis 原生 INCR/DECR 保证原子性；
/// <see cref="TryDeductAsync"/> 通过 Lua 脚本在单命令内完成「检查足够 + 扣减」，防超卖。
/// </summary>
public sealed class RedisCacheService(IConnectionMultiplexer multiplexer) : ICacheService
{
    private readonly IConnectionMultiplexer _multiplexer = multiplexer;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>回源锁 TTL（防止持有锁的请求崩溃导致死锁）</summary>
    private static readonly TimeSpan BackfillLockTtl = TimeSpan.FromSeconds(10);

    /// <summary>GetOrAddAsync 默认缓存 TTL（未显式指定时）</summary>
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromSeconds(60);

    private IDatabase Db => _multiplexer.GetDatabase();

    /// <summary>Lua：仅当剩余值 ≥ delta 时扣减，返回 1；否则不修改返回 0（原子，防超卖）。</summary>
    private const string TryDeductScript = """
        local current = tonumber(redis.call('GET', KEYS[1]) or '0')
        if current >= tonumber(ARGV[1]) then
            redis.call('DECRBY', KEYS[1], ARGV[1])
            return 1
        end
        return 0
        """;

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var value = await Db.StringGetAsync(key);
        if (value.IsNullOrEmpty)
            return default;

        return JsonSerializer.Deserialize<T>((string)value!, JsonOptions);
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await Db.StringSetAsync(key, json, expiry);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken ct = default)
        => await Db.KeyDeleteAsync(key);

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => await Db.KeyExistsAsync(key);

    /// <inheritdoc />
    public async Task<long> IncrementAsync(string key, long delta = 1, CancellationToken ct = default)
        => await Db.StringIncrementAsync(key, delta);

    /// <inheritdoc />
    public async Task<long> DecrementAsync(string key, long delta = 1, CancellationToken ct = default)
        => await Db.StringDecrementAsync(key, delta);

    /// <inheritdoc />
    public async Task<bool> TryDeductAsync(string key, long delta, CancellationToken ct = default)
        => (long)(await Db.ScriptEvaluateAsync(
            TryDeductScript, [key], [delta])) == 1;

    /// <summary>Lua：仅当锁 token 匹配时释放（防止误删他人锁）。</summary>
    private const string ReleaseLockScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        end
        return 0
        """;

    /// <summary>回源锁键（GET 锁成功才回源，双重检查防击穿）</summary>
    private static string BuildBackfillLockKey(string key) => $"backfill:lock:{key}";

    /// <inheritdoc />
    public async Task<T?> GetOrAddAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan? expiry = null,
        CancellationToken ct = default)
    {
        // 快路径：缓存命中直接返回
        var cached = await Db.StringGetAsync(key);
        if (!cached.IsNullOrEmpty)
            return JsonSerializer.Deserialize<T>((string)cached!, JsonOptions);

        // 慢路径：未命中 → 分布式锁 single-flight（仅一个请求回源，其余等待）
        var lockKey = BuildBackfillLockKey(key);
        var lockToken = Guid.NewGuid().ToString("N");
        var acquired = await Db.StringSetAsync(lockKey, lockToken, BackfillLockTtl, When.NotExists);

        if (!acquired)
        {
            // 其他请求正在回源：轮询等待其写入（最多等 3 秒）
            for (var i = 0; i < 30; i++)
            {
                await Task.Delay(100, ct);
                var value = await Db.StringGetAsync(key);
                if (!value.IsNullOrEmpty)
                    return JsonSerializer.Deserialize<T>((string)value!, JsonOptions);
            }
            // 等待超时（源站写入极慢/异常）→ 自己回源兜底，不阻塞业务
        }

        try
        {
            // 拿到锁后二次检查（double-check）：可能在等待期间已被写入
            var recheck = await Db.StringGetAsync(key);
            if (!recheck.IsNullOrEmpty)
                return JsonSerializer.Deserialize<T>((string)recheck!, JsonOptions);

            var result = await factory(ct);
            if (result is not null)
                await SetAsync(key, result, expiry ?? DefaultExpiry, ct);
            return result;
        }
        finally
        {
            // 仅当本次持锁成功才释放（未持锁的兜底回源不释放他人锁）
            if (acquired)
            {
                await Db.ScriptEvaluateAsync(ReleaseLockScript, [lockKey], [lockToken]);
            }
        }
    }
}

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
}

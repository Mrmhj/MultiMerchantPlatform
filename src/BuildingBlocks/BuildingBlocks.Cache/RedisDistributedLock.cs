using StackExchange.Redis;

namespace BuildingBlocks.Cache;

/// <summary>
/// Redis 分布式锁实现 — SET key token NX EX ttl 原子获取；释放用 Lua 校验 token 后 DEL（防误删他人锁）。
/// 锁持有者身份由随机 token 标识，仅持有者可释放。
/// </summary>
public sealed class RedisDistributedLock(IConnectionMultiplexer multiplexer) : IDistributedLock
{
    private readonly IConnectionMultiplexer _multiplexer = multiplexer;

    /// <summary>Lua：仅当锁值等于自身 token 时才删除（防误删他人锁）。</summary>
    private const string ReleaseScript = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        end
        return 0
        """;

    private IDatabase Db => _multiplexer.GetDatabase();

    /// <inheritdoc />
    public async Task<IDistributedLockHandle?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        var token = Guid.NewGuid().ToString("N");
        var acquired = await Db.StringSetAsync(key, token, ttl, When.NotExists);
        return acquired ? new RedisLockHandle(Db, key, token) : null;
    }

    /// <summary>Redis 锁句柄 — 释放时校验 token 后 DEL。</summary>
    private sealed class RedisLockHandle(IDatabase db, string key, string token) : IDistributedLockHandle
    {
        private int _disposed;

        /// <inheritdoc />
        public string Key => key;

        /// <inheritdoc />
        public string Token => token;

        /// <summary>释放锁（Lua 校验 token，防误删）</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                try
                {
                    db.ScriptEvaluate(ReleaseScript, [key], [token]);
                }
                catch
                {
                    // 释放失败让锁自然过期，不抛（Redis 瞬时异常）
                }
            }
        }
    }
}

using System.Collections.Concurrent;

namespace BuildingBlocks.Cache;

/// <summary>
/// 进程内分布式锁实现 — Redis 不可用时的兜底（单机部署语义正确）。
/// 基于 ConcurrentDictionary 存储锁记录（key → token + 过期时间），过期自动让位。
/// </summary>
public sealed class InMemoryDistributedLock : IDistributedLock
{
    private readonly ConcurrentDictionary<string, (string Token, DateTime ExpiresAt)> _locks = new();

    /// <inheritdoc />
    public Task<IDistributedLockHandle?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(ttl);

        // 清理过期锁（尽力而为）
        foreach (var kvp in _locks)
        {
            if (kvp.Value.ExpiresAt <= now && _locks.TryRemove(kvp.Key, out _))
            {
                // 移除成功，继续
            }
        }

        var token = Guid.NewGuid().ToString("N");
        if (!_locks.TryAdd(key, (token, expiresAt)))
            return Task.FromResult<IDistributedLockHandle?>(null);

        return Task.FromResult<IDistributedLockHandle?>(new InMemoryLockHandle(_locks, key, token));
    }

    /// <summary>进程内锁句柄 — 释放时按 token 校验后移除。</summary>
    private sealed class InMemoryLockHandle(
        ConcurrentDictionary<string, (string Token, DateTime ExpiresAt)> locks,
        string key,
        string token) : IDistributedLockHandle
    {
        private int _disposed;

        /// <inheritdoc />
        public string Key => key;

        /// <inheritdoc />
        public string Token => token;

        /// <summary>释放锁（校验 token，仅持有者可释放）</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                if (locks.TryGetValue(key, out var entry) && entry.Token == token)
                    locks.TryRemove(key, out _);
            }
        }
    }
}

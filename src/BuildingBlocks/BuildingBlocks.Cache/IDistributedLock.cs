namespace BuildingBlocks.Cache;

/// <summary>
/// 分布式锁句柄 — 释放锁（实现 IDisposable 供 using 释放）。
/// </summary>
public interface IDistributedLockHandle : IDisposable
{
    /// <summary>锁键</summary>
    string Key { get; }

    /// <summary>锁令牌（防误删他人锁）</summary>
    string Token { get; }
}

/// <summary>
/// 分布式锁接口 — 基于 Redis SETNX 实现（Strategy 模式，Redis 不可用时降级进程内锁）。
/// 秒杀/并发扣减等需要互斥的业务使用；锁须显式释放（using）或自动过期。
/// </summary>
public interface IDistributedLock
{
    /// <summary>尝试获取锁（非阻塞）</summary>
    /// <param name="key">锁键（建议含业务前缀，如 seckill:activity:{id}）</param>
    /// <param name="ttl">锁有效期（到期自动释放，防死锁）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>获取成功返回句柄（using 释放），失败返回 null</returns>
    Task<IDistributedLockHandle?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken ct = default);
}

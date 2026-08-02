namespace BuildingBlocks.Cache;

/// <summary>
/// 缓存服务接口 — 支持 In-Memory / Redis 切换（Strategy 模式）。
/// 除基础读写外，提供原子计数操作（秒杀库存预扣/回补、限流计数等场景使用）。
/// </summary>
public interface ICacheService
{
    /// <summary>读取缓存值（不存在返回默认值）</summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>缓存值或默认值</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>写入缓存值</summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="value">值</param>
    /// <param name="expiry">过期时间（默认不设 = 不过期，慎用）</param>
    /// <param name="ct">取消令牌</param>
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);

    /// <summary>删除缓存键</summary>
    /// <param name="key">缓存键</param>
    /// <param name="ct">取消令牌</param>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>键是否存在</summary>
    /// <param name="key">缓存键</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>是否存在</returns>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);

    /// <summary>原子自增（+delta），返回操作后值；键不存在视为 0 起始。</summary>
    /// <param name="key">缓存键</param>
    /// <param name="delta">增量（可为负）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>操作后的值</returns>
    Task<long> IncrementAsync(string key, long delta = 1, CancellationToken ct = default);

    /// <summary>原子自减（-delta），返回操作后值；键不存在视为 0 起始。</summary>
    /// <param name="key">缓存键</param>
    /// <param name="delta">减量（可为负，即自增）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>操作后的值</returns>
    Task<long> DecrementAsync(string key, long delta = 1, CancellationToken ct = default);

    /// <summary>尝试扣减：仅当剩余值足够时原子扣减 delta 并返回 true，否则不修改并返回 false（防超卖）。</summary>
    /// <param name="key">缓存键（存剩余库存）</param>
    /// <param name="delta">扣减数量</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>是否扣减成功（库存足够）</returns>
    Task<bool> TryDeductAsync(string key, long delta, CancellationToken ct = default);
}

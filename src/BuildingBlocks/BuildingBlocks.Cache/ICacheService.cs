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

    /// <summary>
    /// 读取缓存；未命中时执行 factory 重建并写入（single-flight 防击穿：并发请求只放行一个回源）。
    /// 典型用法：热数据缓存（商品详情/列表、秒杀活动等读多写少场景）。
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">缓存键</param>
    /// <param name="factory">回源工厂（缓存未命中时执行，负责查库/计算）</param>
    /// <param name="expiry">缓存过期时间（默认 60 秒）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>缓存值或 factory 计算结果</returns>
    Task<T?> GetOrAddAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan? expiry = null,
        CancellationToken ct = default);
}

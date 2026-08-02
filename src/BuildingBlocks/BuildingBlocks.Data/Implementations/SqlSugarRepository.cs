using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Results;
using BuildingBlocks.Data.Abstractions;
using SqlSugar;

namespace BuildingBlocks.Data.Implementations;

/// <summary>
/// SqlSugar 仓储实现（Strategy 模式 — SqlSugar 策略）。
/// 依赖注入 ISqlSugarClient（由 AddDataLayer 注册 SqlSugarScope 单例）。
/// </summary>
public class SqlSugarRepository<T>(ISqlSugarClient db, TimeProvider timeProvider) : IRepository<T>
    where T : Entity
{
    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Queryable<T>().FirstAsync(x => x.Id == id);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await db.Queryable<T>().Where(x => !x.IsDeleted).ToListAsync();

    public virtual async Task<PagedResult<T>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var total = await db.Queryable<T>().Where(x => !x.IsDeleted).CountAsync();
        var items = await db.Queryable<T>()
            .Where(x => !x.IsDeleted)
            .ToPageListAsync(page, pageSize);
        return new PagedResult<T>(items, total, page, pageSize);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        entity.CreatedAt = timeProvider.GetUtcNow().DateTime;
        // InsertableByObject 避免 new() 泛型约束（兼容 EF 风格实体）
        await db.InsertableByObject(entity).ExecuteCommandAsync();
        return entity;
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        entity.UpdatedAt = timeProvider.GetUtcNow().DateTime;
        await db.UpdateableByObject(entity).ExecuteCommandAsync();
    }

    public virtual async Task DeleteAsync(T entity, CancellationToken ct = default)
    {
        entity.IsDeleted = true;
        entity.UpdatedAt = timeProvider.GetUtcNow().DateTime;
        await db.UpdateableByObject(entity).ExecuteCommandAsync();
    }

    public virtual async Task<int> CountAsync(CancellationToken ct = default)
        => await db.Queryable<T>().Where(x => !x.IsDeleted).CountAsync();
}

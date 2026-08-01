using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Results;

namespace BuildingBlocks.Data.Abstractions;

/// <summary>
/// 统一仓储接口 — 业务层只依赖此接口，底层 ORM 可切换。
/// </summary>
public interface IRepository<T> where T : Entity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<PagedResult<T>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(T entity, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}

/// <summary>
/// 支持规格模式的仓储接口。
/// </summary>
public interface IRepository<T, TSpec> : IRepository<T> where T : Entity
{
    Task<IReadOnlyList<T>> FindAsync(TSpec specification, CancellationToken ct = default);
}

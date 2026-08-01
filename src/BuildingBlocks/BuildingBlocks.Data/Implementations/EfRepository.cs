using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Results;
using BuildingBlocks.Core.Specifications;
using BuildingBlocks.Data.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Data.Implementations;

/// <summary>
/// EF Core 仓储实现 — 默认 ORM 策略（Repository 模式 + Strategy 模式）。
/// </summary>
public class EfRepository<T>(DbContext context, TimeProvider timeProvider) : IRepository<T>
    where T : Entity
{
    protected readonly DbContext Context = context;
    protected readonly TimeProvider TimeProvider = timeProvider;
    protected DbSet<T> DbSet => Context.Set<T>();

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await DbSet.FindAsync([id], ct);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await DbSet.Where(e => !e.IsDeleted).ToListAsync(ct);

    public virtual async Task<PagedResult<T>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet.Where(e => !e.IsDeleted);
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return new PagedResult<T>(items, total, page, pageSize);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        await DbSet.AddAsync(entity, ct);
        return entity;
    }

    public virtual Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        entity.UpdatedAt = TimeProvider.GetUtcNow().DateTime;
        Context.Entry(entity).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(T entity, CancellationToken ct = default)
    {
        entity.IsDeleted = true;
        entity.UpdatedAt = TimeProvider.GetUtcNow().DateTime;
        return Task.CompletedTask;
    }

    public virtual async Task<int> CountAsync(CancellationToken ct = default)
        => await DbSet.CountAsync(e => !e.IsDeleted, ct);
}

/// <summary>
/// 支持规格模式的 EF Core 仓储实现（Specification 模式）。
/// </summary>
public class EfSpecificationRepository<T>(DbContext context, TimeProvider timeProvider)
    : EfRepository<T>(context, timeProvider), ISpecificationRepository<T>
    where T : Entity
{
    public virtual async Task<IReadOnlyList<T>> FindAsync(ISpecification<T> specification, CancellationToken ct = default)
    {
        var query = DbSet.Where(e => !e.IsDeleted).Where(specification.ToExpression());
        return await query.ToListAsync(ct);
    }

    public virtual async Task<PagedResult<T>> FindPagedAsync(ISpecification<T> specification, int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet.Where(e => !e.IsDeleted).Where(specification.ToExpression());
        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return new PagedResult<T>(items, total, page, pageSize);
    }

    public virtual async Task<int> CountAsync(ISpecification<T> specification, CancellationToken ct = default)
        => await DbSet.Where(e => !e.IsDeleted).CountAsync(specification.ToExpression(), ct);
}

using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Results;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Data.Implementations;

/// <summary>
/// EF Core 仓储实现 — 默认 ORM。
/// </summary>
public class EfRepository<T> : Abstractions.IRepository<T> where T : Entity
{
    protected readonly DbContext Context;
    protected readonly DbSet<T> DbSet;

    public EfRepository(DbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await DbSet.FindAsync(new object[] { id }, ct);
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        return await DbSet.Where(e => !e.IsDeleted).ToListAsync(ct);
    }

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
        entity.UpdatedAt = DateTime.UtcNow;
        Context.Entry(entity).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(T entity, CancellationToken ct = default)
    {
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public virtual async Task<int> CountAsync(CancellationToken ct = default)
    {
        return await DbSet.CountAsync(e => !e.IsDeleted, ct);
    }
}

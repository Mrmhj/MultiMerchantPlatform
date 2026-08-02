using System.Data;
using System.Reflection;
using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Results;
using BuildingBlocks.Data.Abstractions;
using Dapper;

namespace BuildingBlocks.Data.Implementations;

/// <summary>
/// Dapper 仓储实现（Strategy 模式 — Dapper 策略）。
/// 适用场景：性能热点 SQL、外部系统存储过程调用、无 EF 模型的表。
/// 基础 CRUD 通过反射生成 SQL；复杂查询请使用 IDbConnectionSwitcher 直接执行。
/// </summary>
public class DapperRepository<T>(IDbConnectionSwitcher switcher, TimeProvider timeProvider) : IRepository<T>
    where T : Entity
{
    protected virtual string TableName => typeof(T).Name;

    protected virtual IDbConnection CreateConnection() => switcher.GetDefaultConnection();

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<T>(
            $"SELECT * FROM [{TableName}] WHERE Id = @Id", new { Id = id });
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync<T>(
            $"SELECT * FROM [{TableName}] WHERE IsDeleted = 0");
        return rows.ToList();
    }

    public virtual async Task<PagedResult<T>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        var offset = (page - 1) * pageSize;
        var total = await conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM [{TableName}] WHERE IsDeleted = 0");
        var items = await conn.QueryAsync<T>(
            $"SELECT * FROM [{TableName}] WHERE IsDeleted = 0 ORDER BY CreatedAt OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            new { Offset = offset, PageSize = pageSize });
        return new PagedResult<T>(items.ToList(), total, page, pageSize);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        entity.CreatedAt = timeProvider.GetUtcNow().DateTime;
        var (columns, values, param) = BuildInsert(entity);
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            $"INSERT INTO [{TableName}] ({columns}) VALUES ({values})", param);
        return entity;
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        entity.UpdatedAt = timeProvider.GetUtcNow().DateTime;
        var param = BuildUpdate(entity);
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            $"UPDATE [{TableName}] SET {param.SetClause} WHERE Id = @Id", param.Parameters);
    }

    public virtual async Task DeleteAsync(T entity, CancellationToken ct = default)
    {
        entity.IsDeleted = true;
        entity.UpdatedAt = timeProvider.GetUtcNow().DateTime;
        await UpdateAsync(entity, ct);
    }

    public virtual async Task<int> CountAsync(CancellationToken ct = default)
    {
        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM [{TableName}] WHERE IsDeleted = 0");
    }

    // ── 反射 SQL 构建 ──

    private static readonly PropertyInfo[] MappedProperties =
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.Name != nameof(Entity.DomainEvents))
            .ToArray();

    private static (string Columns, string Values, object Parameters) BuildInsert(T entity)
    {
        var param = new Dictionary<string, object?> { ["Id"] = entity.Id };
        var cols = new List<string> { "[Id]" };
        var vals = new List<string> { "@Id" };

        foreach (var prop in MappedProperties)
        {
            if (prop.Name == nameof(Entity.DomainEvents))
                continue;
            param[prop.Name] = prop.GetValue(entity);
            cols.Add($"[{prop.Name}]");
            vals.Add($"@{prop.Name}");
        }

        return (string.Join(", ", cols), string.Join(", ", vals), param);
    }

    private static (string SetClause, object Parameters) BuildUpdate(T entity)
    {
        var param = new Dictionary<string, object?> { ["Id"] = entity.Id };
        var set = new List<string>();

        foreach (var prop in MappedProperties)
        {
            if (prop.Name is nameof(Entity.DomainEvents) or nameof(Entity.Id) or nameof(Entity.CreatedAt))
                continue;
            param[prop.Name] = prop.GetValue(entity);
            set.Add($"[{prop.Name}] = @{prop.Name}");
        }

        return (string.Join(", ", set), param);
    }
}

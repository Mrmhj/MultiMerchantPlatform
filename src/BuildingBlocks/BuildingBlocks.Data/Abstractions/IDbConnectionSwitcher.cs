using System.Data;

namespace BuildingBlocks.Data.Abstractions;

/// <summary>
/// 数据库连接切换器 — 支持按名称切换不同数据库连接（对接外部系统）。
/// </summary>
public interface IDbConnectionSwitcher
{
    /// <summary>获取指定名称的数据库连接</summary>
    IDbConnection GetConnection(string name);

    /// <summary>获取默认数据库连接</summary>
    IDbConnection GetDefaultConnection();

    /// <summary>注册一个命名连接</summary>
    void RegisterConnection(string name, string connectionString);
}

/// <summary>
/// 工作单元接口 — 管理事务。
/// </summary>
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

using System.Data;
using Microsoft.Data.SqlClient;
using BuildingBlocks.Data.Options;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Data.Implementations;

/// <summary>
/// 数据库连接切换器实现 — 支持按名称切换连接，方便对接外部系统。
/// </summary>
public class DbConnectionSwitcher : Abstractions.IDbConnectionSwitcher
{
    private readonly Dictionary<string, string> _connectionStrings;
    private readonly string _defaultName;

    public DbConnectionSwitcher(IOptions<DataOptions> options)
    {
        _connectionStrings = new Dictionary<string, string>(options.Value.Connections, StringComparer.OrdinalIgnoreCase);
        _defaultName = options.Value.DefaultConnectionName;
    }

    public IDbConnection GetConnection(string name)
    {
        if (!_connectionStrings.TryGetValue(name, out var connStr))
            throw new InvalidOperationException($"未找到名为 '{name}' 的数据库连接配置。");

        return new SqlConnection(connStr);
    }

    public IDbConnection GetDefaultConnection()
    {
        return GetConnection(_defaultName);
    }

    public void RegisterConnection(string name, string connectionString)
    {
        _connectionStrings[name] = connectionString;
    }
}

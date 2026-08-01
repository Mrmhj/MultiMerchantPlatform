using System.Data;
using BuildingBlocks.Data.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Data.Implementations;

/// <summary>
/// 数据库连接切换器实现 — 支持按名称切换连接（Factory 模式）。
/// </summary>
public class DbConnectionSwitcher(IOptions<DataOptions> options) : Abstractions.IDbConnectionSwitcher
{
    private readonly Dictionary<string, string> _connectionStrings
        = new(options.Value.Connections, StringComparer.OrdinalIgnoreCase);
    private readonly string _defaultName = options.Value.DefaultConnectionName;

    public IDbConnection GetConnection(string name)
    {
        if (!_connectionStrings.TryGetValue(name, out var connStr))
            throw new InvalidOperationException($"未找到名为 '{name}' 的数据库连接配置。");

        return new SqlConnection(connStr);
    }

    public IDbConnection GetDefaultConnection() => GetConnection(_defaultName);

    public void RegisterConnection(string name, string connectionString)
        => _connectionStrings[name] = connectionString;
}

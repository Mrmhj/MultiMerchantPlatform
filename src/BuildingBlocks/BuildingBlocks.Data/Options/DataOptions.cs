namespace BuildingBlocks.Data.Options;

/// <summary>
/// 数据层配置选项。
/// </summary>
public record DataOptions
{
    /// <summary>默认 ORM 类型</summary>
    public OrmType DefaultOrm { get; init; } = OrmType.EfCore;

    /// <summary>数据库连接字典</summary>
    public Dictionary<string, string> Connections { get; init; } = [];

    /// <summary>默认连接名称</summary>
    public string DefaultConnectionName { get; init; } = "Default";
}

/// <summary>
/// 支持的 ORM 类型（Strategy 模式 — 可切换的数据访问策略）。
/// </summary>
public enum OrmType
{
    /// <summary>Entity Framework Core 10（默认）</summary>
    EfCore,

    /// <summary>SqlSugar（国内场景，Code First）</summary>
    SqlSugar,

    /// <summary>Dapper（性能热点 + 外部系统存储过程）</summary>
    Dapper
}

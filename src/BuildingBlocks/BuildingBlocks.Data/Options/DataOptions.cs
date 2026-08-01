namespace BuildingBlocks.Data.Options;

/// <summary>
/// 数据层配置选项。
/// </summary>
public class DataOptions
{
    /// <summary>默认 ORM 类型</summary>
    public OrmType DefaultOrm { get; set; } = OrmType.EfCore;

    /// <summary>数据库连接字典</summary>
    public Dictionary<string, string> Connections { get; set; } = new();

    /// <summary>默认连接名称</summary>
    public string DefaultConnectionName { get; set; } = "Default";
}

/// <summary>
/// 支持的 ORM 类型。
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

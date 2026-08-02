using BuildingBlocks.Data.Options;

namespace BuildingBlocks.Data.Strategies;

/// <summary>
/// ORM 策略接口（Strategy 模式）— 标记当前生效的数据访问策略。
/// </summary>
public interface IOrmStrategy
{
    OrmType Type { get; }
}

/// <summary>EF Core 策略</summary>
public sealed class EfCoreOrmStrategy : IOrmStrategy
{
    public OrmType Type => OrmType.EfCore;
}

/// <summary>SqlSugar 策略</summary>
public sealed class SqlSugarOrmStrategy : IOrmStrategy
{
    public OrmType Type => OrmType.SqlSugar;
}

/// <summary>Dapper 策略</summary>
public sealed class DapperOrmStrategy : IOrmStrategy
{
    public OrmType Type => OrmType.Dapper;
}

using BuildingBlocks.Core.Entities;
using BuildingBlocks.Data.Abstractions;

namespace BuildingBlocks.Data.Abstractions;

/// <summary>
/// 仓储工厂接口（Factory 模式）— 按配置的 ORM 策略创建统一仓储。
/// 业务层可注入 IRepositoryFactory 动态获取仓储，无需感知底层 ORM。
/// </summary>
public interface IRepositoryFactory
{
    /// <summary>创建统一仓储（按 DataOptions.DefaultOrm 选择实现）</summary>
    IRepository<T> Create<T>() where T : Entity;

    /// <summary>创建支持规格模式的仓储（仅 EF Core 策略支持）</summary>
    ISpecificationRepository<T> CreateSpecification<T>() where T : Entity;
}

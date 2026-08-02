using BuildingBlocks.Core.Entities;
using BuildingBlocks.Data.Abstractions;
using BuildingBlocks.Data.Options;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Data.Implementations;

/// <summary>
/// 仓储工厂实现（Factory 模式）— 按 DataOptions.DefaultOrm 从 DI 容器解析对应实现。
/// </summary>
public sealed class RepositoryFactory(
    IServiceProvider serviceProvider,
    DataOptions options) : IRepositoryFactory
{
    /// <inheritdoc />
    public IRepository<T> Create<T>() where T : Entity
        => options.DefaultOrm switch
        {
            OrmType.SqlSugar => serviceProvider.GetRequiredService<SqlSugarRepository<T>>(),
            OrmType.Dapper => serviceProvider.GetRequiredService<DapperRepository<T>>(),
            _ => serviceProvider.GetRequiredService<EfRepository<T>>(),
        };

    /// <inheritdoc />
    public ISpecificationRepository<T> CreateSpecification<T>() where T : Entity
        => options.DefaultOrm switch
        {
            OrmType.EfCore => serviceProvider.GetRequiredService<EfSpecificationRepository<T>>(),
            _ => throw new NotSupportedException(
                $"OrmType {options.DefaultOrm} 暂不支持规格模式（Specification）仓储，请使用 EF Core 或改用 IRepository<T>"),
        };
}

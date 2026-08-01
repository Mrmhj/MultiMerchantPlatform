namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// CQRS 命令接口 — 表示一个改变系统状态的操作。
/// </summary>
public interface ICommand<TResult> { }

public interface ICommand : ICommand<Unit> { }

/// <summary>
/// CQRS 查询接口 — 表示一个只读操作。
/// </summary>
public interface IQuery<TResult> { }

/// <summary>
/// 命令处理器接口。
/// </summary>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// 无返回值的命令处理器。
/// </summary>
public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Unit>
    where TCommand : ICommand
{
}

/// <summary>
/// 查询处理器接口。
/// </summary>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// 无返回值标记。
/// </summary>
public record Unit;

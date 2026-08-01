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

/// <summary>
/// 中介者接口 — 统一调度命令和查询（Mediator 模式）。
/// 各服务通过 IMediator 发送命令/查询，由中介者路由到对应 Handler。
/// </summary>
public interface IMediator
{
    /// <summary>发送命令</summary>
    Task<TResult> SendAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : ICommand<TResult>;

    /// <summary>发送查询</summary>
    Task<TResult> QueryAsync<TQuery, TResult>(TQuery query, CancellationToken ct = default)
        where TQuery : IQuery<TResult>;
}

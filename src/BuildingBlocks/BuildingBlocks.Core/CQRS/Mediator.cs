using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Core.CQRS;

/// <summary>
/// 中介者默认实现 — 从 DI 容器解析命令/查询对应的 Handler 并调用（Mediator 模式）。
/// 业务代码通过 <see cref="IMediator"/> 发送 Command / Query，由本类路由到实现类，
/// Controller 不直接依赖业务服务，实现解耦。
/// </summary>
public sealed class Mediator(IServiceProvider serviceProvider) : IMediator
{
    /// <inheritdoc />
    public async Task<TResult> SendAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : ICommand<TResult>
    {
        var handler = serviceProvider.GetRequiredService<ICommandHandler<TCommand, TResult>>();
        return await handler.HandleAsync(command, ct);
    }

    /// <inheritdoc />
    public async Task<TResult> QueryAsync<TQuery, TResult>(TQuery query, CancellationToken ct = default)
        where TQuery : IQuery<TResult>
    {
        var handler = serviceProvider.GetRequiredService<IQueryHandler<TQuery, TResult>>();
        return await handler.HandleAsync(query, ct);
    }
}

/// <summary>
/// 中介者依赖注入注册。
/// </summary>
public static class MediatorServiceCollectionExtensions
{
    /// <summary>注册中介者（Scoped，与请求生命周期一致）</summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddMediator(this IServiceCollection services)
        => services.AddScoped<IMediator, Mediator>();
}

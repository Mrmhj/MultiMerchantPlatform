namespace BuildingBlocks.Core.Events;

/// <summary>
/// 领域事件接口 — 由聚合根产生，由事件处理器消费。
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
}

/// <summary>
/// 领域事件基类。
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// 集成事件接口 — 跨服务传递的事件（通过 messaging-service）。
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredOn { get; }
    string EventName { get; }
}

/// <summary>
/// 集成事件基类。
/// </summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
    public string EventName => GetType().Name;
}

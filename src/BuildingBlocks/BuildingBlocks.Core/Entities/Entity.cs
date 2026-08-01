namespace BuildingBlocks.Core.Entities;

/// <summary>
/// 聚合根标记接口 — DDD 聚合根必须实现此接口。
/// </summary>
public interface IAggregateRoot { }

/// <summary>
/// 实体基类 — 所有领域实体的根。
/// 包含领域事件集合（Observer 模式 / Aggregate Root 模式）。
/// </summary>
public abstract class Entity
{
    private readonly List<Events.IDomainEvent> _domainEvents = [];

    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    /// <summary>领域事件集合（只读视图）</summary>
    public IReadOnlyList<Events.IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>添加领域事件</summary>
    protected void AddDomainEvent(Events.IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>清除已处理的领域事件</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    public override bool Equals(object? obj) =>
        obj is Entity other
        && (ReferenceEquals(this, other) || (GetType() == other.GetType() && Id == other.Id));

    public override int GetHashCode() => Id.GetHashCode() * 31;

    public static bool operator ==(Entity? a, Entity? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(Entity? a, Entity? b) => !(a == b);
}

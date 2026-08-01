namespace BuildingBlocks.Core.Entities;

/// <summary>
/// 实体基类 — 所有领域实体的根。
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        return Id == other.Id;
    }

    public override int GetHashCode() => Id.GetHashCode() * 31;

    public static bool operator ==(Entity? a, Entity? b) =>
        a?.Equals(b) ?? b is null;

    public static bool operator !=(Entity? a, Entity? b) => !(a == b);
}

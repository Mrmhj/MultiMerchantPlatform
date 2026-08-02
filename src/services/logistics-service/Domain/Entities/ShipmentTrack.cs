using BuildingBlocks.Core.Entities;
using LogisticsService.Domain.Enums;

namespace LogisticsService.Domain.Entities;

/// <summary>
/// 运单轨迹记录 — 一条运单多条轨迹（时间升序追加）。
/// </summary>
public sealed class ShipmentTrack : Entity
{
    private ShipmentTrack() { } // EF Core

    /// <summary>创建轨迹记录</summary>
    /// <param name="shipmentId">运单 ID</param>
    /// <param name="status">状态（快照）</param>
    /// <param name="description">轨迹描述</param>
    /// <param name="location">地点（可选）</param>
    /// <param name="trackedAt">轨迹时间（UTC）</param>
    public ShipmentTrack(Guid shipmentId, ShipmentStatus status, string description, string? location, DateTime trackedAt)
    {
        ShipmentId = shipmentId;
        Status = status;
        Description = description;
        Location = location;
        TrackedAt = trackedAt;
    }

    /// <summary>所属运单 ID</summary>
    public Guid ShipmentId { get; private set; }

    /// <summary>状态（快照）</summary>
    public ShipmentStatus Status { get; private set; }

    /// <summary>轨迹描述</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>地点（可选）</summary>
    public string? Location { get; private set; }

    /// <summary>轨迹时间（UTC）</summary>
    public DateTime TrackedAt { get; private set; }
}

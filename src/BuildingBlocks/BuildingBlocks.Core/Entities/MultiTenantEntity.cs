namespace BuildingBlocks.Core.Entities;

/// <summary>
/// 多商户实体基类 — 所有需要商户隔离的实体继承此类。
/// </summary>
public abstract class MultiTenantEntity : Entity
{
    /// <summary>所属商户 ID</summary>
    public required Guid MerchantId { get; set; }
}

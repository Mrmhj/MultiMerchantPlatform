namespace BuildingBlocks.MultiTenant;

/// <summary>
/// 租户（商户）提供者接口 — 各服务通过此接口获取当前请求的商户 ID。
/// </summary>
public interface ITenantProvider
{
    Guid? CurrentMerchantId { get; }
    Guid? CurrentUserId { get; }
    bool IsPlatformAdmin { get; }
}

/// <summary>
/// 租户上下文 — 请求作用域内保存商户信息。
/// </summary>
public record TenantContext : ITenantProvider
{
    public Guid? CurrentMerchantId { get; init; }
    public Guid? CurrentUserId { get; init; }
    public bool IsPlatformAdmin { get; init; }

    public static TenantContext Empty => new();
}

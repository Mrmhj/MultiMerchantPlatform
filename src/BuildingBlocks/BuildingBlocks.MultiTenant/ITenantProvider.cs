namespace BuildingBlocks.MultiTenant;

/// <summary>
/// 租户（商户）提供者接口 — 各服务通过此接口获取当前请求的商户 ID。
/// </summary>
public interface ITenantProvider
{
    /// <summary>当前请求的商户 ID</summary>
    Guid? CurrentMerchantId { get; }

    /// <summary>当前用户 ID</summary>
    Guid? CurrentUserId { get; }

    /// <summary>是否为平台管理员</summary>
    bool IsPlatformAdmin { get; }
}

/// <summary>
/// 租户上下文 — 请求作用域内保存商户信息。
/// </summary>
public class TenantContext : ITenantProvider
{
    public Guid? CurrentMerchantId { get; set; }
    public Guid? CurrentUserId { get; set; }
    public bool IsPlatformAdmin { get; set; }

    public static TenantContext Empty => new();
}

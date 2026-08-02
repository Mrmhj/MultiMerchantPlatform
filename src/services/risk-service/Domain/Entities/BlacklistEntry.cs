using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;
using RiskService.Domain.Enums;

namespace RiskService.Domain.Entities;

/// <summary>
/// 黑名单 — 平台级管控：用户 / IP / 设备 加入黑名单后，决策接口直接拦截。
/// 支持过期时间（ExpiresAt 为空表示永久）。
/// </summary>
public sealed class BlacklistEntry : Entity, IAggregateRoot
{
    private BlacklistEntry() { } // EF Core

    /// <summary>加入黑名单</summary>
    /// <param name="targetType">对象类型（用户/IP/设备）</param>
    /// <param name="targetValue">对象值（用户 ID 字符串 / IP / 设备 ID）</param>
    /// <param name="reason">拉黑原因</param>
    /// <param name="expiresAt">过期时间（UTC，null = 永久）</param>
    /// <param name="merchantId">商户 ID（null = 平台全局黑名单）</param>
    public BlacklistEntry(BlacklistTargetType targetType, string targetValue, string reason,
        DateTime? expiresAt = null, Guid? merchantId = null)
    {
        TargetType = targetType;
        TargetValue = (targetValue ?? string.Empty).Trim();
        Reason = (reason ?? string.Empty).Trim();
        ExpiresAt = expiresAt;
        MerchantId = merchantId;
        Enabled = true;
        CreatedAt = DateTime.UtcNow;
        if (string.IsNullOrEmpty(TargetValue))
            throw new DomainException("黑名单对象值不能为空", "INVALID_BLACKLIST_VALUE");
        if (string.IsNullOrEmpty(Reason))
            throw new DomainException("拉黑原因不能为空", "INVALID_BLACKLIST_REASON");
    }

    /// <summary>对象类型（用户/IP/设备）</summary>
    public BlacklistTargetType TargetType { get; private set; }

    /// <summary>对象值（用户 ID 字符串 / IP / 设备 ID）</summary>
    public string TargetValue { get; private set; } = string.Empty;

    /// <summary>拉黑原因</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>过期时间（UTC，null = 永久）</summary>
    public DateTime? ExpiresAt { get; private set; }

    /// <summary>商户 ID（null = 平台全局黑名单）</summary>
    public Guid? MerchantId { get; private set; }

    /// <summary>是否启用（停用后不参与拦截）</summary>
    public bool Enabled { get; private set; }

    /// <summary>是否已过期</summary>
    /// <param name="now">当前时间（UTC）</param>
    /// <returns>true 表示已过期</returns>
    public bool IsExpired(DateTime now) => ExpiresAt.HasValue && ExpiresAt.Value <= now;

    /// <summary>启用黑名单</summary>
    public void Enable() => Enabled = true;

    /// <summary>停用黑名单（不再参与拦截）</summary>
    public void Disable() => Enabled = false;

    /// <summary>更新黑名单（原因 / 有效期），并重新启用（同对象重复拉黑时调用）</summary>
    /// <param name="reason">拉黑原因</param>
    /// <param name="expiresAt">过期时间（UTC，null = 永久）</param>
    public void Update(string reason, DateTime? expiresAt)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("拉黑原因不能为空", "INVALID_BLACKLIST_REASON");
        Reason = reason.Trim();
        ExpiresAt = expiresAt;
        Enabled = true;
        UpdatedAt = DateTime.UtcNow;
    }
}

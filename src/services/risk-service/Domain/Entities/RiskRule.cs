using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;
using RiskService.Domain.Enums;

namespace RiskService.Domain.Entities;

/// <summary>
/// 风控规则 — 规则引擎的核心配置：某场景下按维度在时间窗口内聚合事件，超过阈值即命中。
/// 平台级配置，MerchantId 为空表示全局规则（所有商户生效），非空表示仅该商户生效。
/// </summary>
public sealed class RiskRule : Entity, IAggregateRoot
{
    private RiskRule() { } // EF Core

    /// <summary>创建风控规则</summary>
    /// <param name="name">规则名称</param>
    /// <param name="scene">场景编码（如 ORDER_SUBMIT / COUPON_CLAIM / LOGIN_FAIL）</param>
    /// <param name="dimension">统计维度（用户/IP/设备/商户）</param>
    /// <param name="windowSeconds">时间窗口（秒）</param>
    /// <param name="threshold">窗口内命中阈值（次数）</param>
    /// <param name="disposition">处置级别（观察/拦截）</param>
    /// <param name="merchantId">商户 ID（null = 全局规则）</param>
    /// <param name="description">规则说明（可选）</param>
    [SetsRequiredMembers]
    public RiskRule(string name, string scene, RiskDimension dimension, int windowSeconds, int threshold,
        RiskDisposition disposition, Guid? merchantId = null, string? description = null)
    {
        Name = ValidateName(name);
        Scene = ValidateScene(scene);
        Dimension = dimension;
        WindowSeconds = ValidateWindow(windowSeconds);
        Threshold = ValidateThreshold(threshold);
        Disposition = disposition;
        MerchantId = merchantId;
        Description = description?.Trim();
        Enabled = true;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>规则名称</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>场景编码（ORDER_SUBMIT / COUPON_CLAIM / LOGIN_FAIL / REVIEW_SUBMIT 等）</summary>
    public string Scene { get; private set; } = string.Empty;

    /// <summary>统计维度</summary>
    public RiskDimension Dimension { get; private set; }

    /// <summary>时间窗口（秒）</summary>
    public int WindowSeconds { get; private set; }

    /// <summary>窗口内命中阈值（次数）</summary>
    public int Threshold { get; private set; }

    /// <summary>处置级别（观察/拦截）</summary>
    public RiskDisposition Disposition { get; private set; }

    /// <summary>商户 ID（null = 全局规则）</summary>
    public Guid? MerchantId { get; private set; }

    /// <summary>规则说明</summary>
    public string? Description { get; private set; }

    /// <summary>是否启用</summary>
    public bool Enabled { get; private set; }

    /// <summary>更新规则配置</summary>
    /// <param name="name">规则名称</param>
    /// <param name="scene">场景编码</param>
    /// <param name="dimension">统计维度</param>
    /// <param name="windowSeconds">时间窗口（秒）</param>
    /// <param name="threshold">窗口内命中阈值</param>
    /// <param name="disposition">处置级别</param>
    /// <param name="merchantId">商户 ID（null = 全局）</param>
    /// <param name="description">规则说明</param>
    public void Update(string name, string scene, RiskDimension dimension, int windowSeconds, int threshold,
        RiskDisposition disposition, Guid? merchantId, string? description)
    {
        Name = ValidateName(name);
        Scene = ValidateScene(scene);
        Dimension = dimension;
        WindowSeconds = ValidateWindow(windowSeconds);
        Threshold = ValidateThreshold(threshold);
        Disposition = disposition;
        MerchantId = merchantId;
        Description = description?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>启用规则</summary>
    public void Enable() => Enabled = true;

    /// <summary>停用规则（停用后不再参与匹配）</summary>
    public void Disable() => Enabled = false;

    private static string ValidateName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length is < 2 or > 100)
            throw new DomainException("规则名称长度需在 2-100 字符之间", "INVALID_RULE_NAME");
        return trimmed;
    }

    private static string ValidateScene(string scene)
    {
        var trimmed = (scene ?? string.Empty).Trim().ToUpperInvariant();
        if (trimmed.Length is < 2 or > 50)
            throw new DomainException("场景编码长度需在 2-50 字符之间", "INVALID_RULE_SCENE");
        return trimmed;
    }

    private static int ValidateWindow(int windowSeconds)
    {
        if (windowSeconds is < 1 or > 86400)
            throw new DomainException("时间窗口需在 1-86400 秒之间", "INVALID_WINDOW_SECONDS");
        return windowSeconds;
    }

    private static int ValidateThreshold(int threshold)
    {
        if (threshold is < 1 or > 100000)
            throw new DomainException("命中阈值需在 1-100000 之间", "INVALID_THRESHOLD");
        return threshold;
    }
}

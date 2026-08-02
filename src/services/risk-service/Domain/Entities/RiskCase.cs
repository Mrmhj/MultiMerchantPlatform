using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;
using RiskService.Domain.Enums;

namespace RiskService.Domain.Entities;

/// <summary>
/// 风险案例 — 规则命中后生成的风险记录，供平台风控人员处置。
/// 状态机：Open（待处置）→ Reviewing（人工复核中）→ Resolved（确认风险）/ FalsePositive（误报）。
/// </summary>
public sealed class RiskCase : Entity, IAggregateRoot
{
    private RiskCase() { } // EF Core

    /// <summary>创建风险案例（规则命中时生成，初始 Open）</summary>
    /// <param name="ruleId">命中的规则 ID（黑名单拦截可为 null）</param>
    /// <param name="ruleName">规则名称（快照）</param>
    /// <param name="scene">场景编码（快照）</param>
    /// <param name="dimension">统计维度（快照）</param>
    /// <param name="dimensionKey">命中维度键（如用户 ID 字符串 / IP / 设备 ID）</param>
    /// <param name="userId">用户 ID（可选）</param>
    /// <param name="merchantId">商户 ID（可选）</param>
    /// <param name="ip">客户端 IP（可选）</param>
    /// <param name="deviceId">设备 ID（可选）</param>
    /// <param name="occurredCount">窗口内发生次数</param>
    /// <param name="threshold">规则阈值（快照）</param>
    /// <param name="disposition">处置级别（快照）</param>
    /// <param name="source">来源（RULE_HIT / BLACKLIST）</param>
    /// <param name="summary">风险摘要（快照，如「60秒内下单5次」）</param>
    [SetsRequiredMembers]
    public RiskCase(Guid? ruleId, string ruleName, string scene, RiskDimension dimension, string dimensionKey,
        Guid? userId, Guid? merchantId, string? ip, string? deviceId,
        int occurredCount, int threshold, RiskDisposition disposition, string source, string summary)
    {
        RuleId = ruleId;
        RuleName = ruleName;
        Scene = (scene ?? string.Empty).Trim().ToUpperInvariant();
        Dimension = dimension;
        DimensionKey = (dimensionKey ?? string.Empty).Trim();
        UserId = userId;
        MerchantId = merchantId;
        Ip = ip?.Trim();
        DeviceId = deviceId?.Trim();
        OccurredCount = occurredCount;
        Threshold = threshold;
        Disposition = disposition;
        Source = (source ?? string.Empty).Trim().ToUpperInvariant();
        Summary = summary;
        Status = RiskCaseStatus.Open;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>命中的规则 ID（黑名单拦截为 null）</summary>
    public Guid? RuleId { get; private set; }

    /// <summary>规则名称（快照）</summary>
    public string RuleName { get; private set; } = string.Empty;

    /// <summary>场景编码（快照）</summary>
    public string Scene { get; private set; } = string.Empty;

    /// <summary>统计维度（快照）</summary>
    public RiskDimension Dimension { get; private set; }

    /// <summary>命中维度键（用户 ID 字符串 / IP / 设备 ID）</summary>
    public string DimensionKey { get; private set; } = string.Empty;

    /// <summary>用户 ID（可选）</summary>
    public Guid? UserId { get; private set; }

    /// <summary>商户 ID（可选）</summary>
    public Guid? MerchantId { get; private set; }

    /// <summary>客户端 IP（可选）</summary>
    public string? Ip { get; private set; }

    /// <summary>设备 ID（可选）</summary>
    public string? DeviceId { get; private set; }

    /// <summary>窗口内发生次数</summary>
    public int OccurredCount { get; private set; }

    /// <summary>规则阈值（快照）</summary>
    public int Threshold { get; private set; }

    /// <summary>处置级别（快照）</summary>
    public RiskDisposition Disposition { get; private set; }

    /// <summary>来源（RULE_HIT / BLACKLIST）</summary>
    public string Source { get; private set; } = "RULE_HIT";

    /// <summary>风险摘要（快照）</summary>
    public string Summary { get; private set; } = string.Empty;

    /// <summary>状态（Open → Reviewing → Resolved / FalsePositive）</summary>
    public RiskCaseStatus Status { get; private set; }

    /// <summary>处置备注</summary>
    public string? ResolutionNote { get; private set; }

    /// <summary>处置时间（未处置为 null）</summary>
    public DateTime? ResolvedAt { get; private set; }

    /// <summary>追加命中次数（窗口内同一维度再次命中时累加）</summary>
    /// <param name="additional">本次新增次数</param>
    public void IncreaseCount(int additional = 1)
    {
        if (Status is not (RiskCaseStatus.Open or RiskCaseStatus.Reviewing))
            throw new DomainException($"案例已处置（{Status}），不能追加次数", "RISK_CASE_CLOSED");
        OccurredCount += Math.Max(0, additional);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>开始人工复核（Open → Reviewing）</summary>
    public void StartReview()
    {
        if (Status != RiskCaseStatus.Open)
            throw new DomainException($"当前状态不允许复核（{Status}）", "RISK_CASE_STATE_INVALID");
        Status = RiskCaseStatus.Reviewing;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>确认风险（Open/Reviewing → Resolved）</summary>
    /// <param name="note">处置备注</param>
    /// <param name="now">处置时间（UTC）</param>
    public void Resolve(string? note, DateTime now)
    {
        if (Status is not (RiskCaseStatus.Open or RiskCaseStatus.Reviewing))
            throw new DomainException($"当前状态不允许确认风险（{Status}）", "RISK_CASE_STATE_INVALID");

        Status = RiskCaseStatus.Resolved;
        ResolutionNote = (note ?? string.Empty).Trim();
        ResolvedAt = now;
        UpdatedAt = now;
    }

    /// <summary>标记误报（Open/Reviewing → FalsePositive）</summary>
    /// <param name="note">误报说明</param>
    /// <param name="now">处置时间（UTC）</param>
    public void MarkFalsePositive(string? note, DateTime now)
    {
        if (Status is not (RiskCaseStatus.Open or RiskCaseStatus.Reviewing))
            throw new DomainException($"当前状态不允许标记误报（{Status}）", "RISK_CASE_STATE_INVALID");

        Status = RiskCaseStatus.FalsePositive;
        ResolutionNote = (note ?? string.Empty).Trim();
        ResolvedAt = now;
        UpdatedAt = now;
    }
}

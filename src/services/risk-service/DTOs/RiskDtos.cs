using System.ComponentModel.DataAnnotations;
using RiskService.Domain.Enums;

namespace RiskService.DTOs;

/// <summary>风控事件上报请求项（内部接口）</summary>
public sealed record SubmitRiskEventRequest
{
    /// <summary>场景编码（ORDER_SUBMIT / COUPON_CLAIM / LOGIN_FAIL / REVIEW_SUBMIT）</summary>
    [Required, MaxLength(50)]
    public string Scene { get; init; } = string.Empty;

    /// <summary>用户 ID（可选）</summary>
    public Guid? UserId { get; init; }

    /// <summary>商户 ID（可选）</summary>
    public Guid? MerchantId { get; init; }

    /// <summary>客户端 IP（可选）</summary>
    [MaxLength(64)]
    public string? Ip { get; init; }

    /// <summary>设备 ID（可选）</summary>
    [MaxLength(128)]
    public string? DeviceId { get; init; }

    /// <summary>附加载荷 JSON（可选）</summary>
    [MaxLength(8000)]
    public string? PayloadJson { get; init; }

    /// <summary>事件发生时间（UTC，可选，默认当前时间）</summary>
    public DateTime? OccurredAt { get; init; }
}

/// <summary>事件上报响应（内部接口）— 规则引擎评估结果</summary>
public sealed record SubmitRiskEventResponse
{
    /// <summary>上报事件数</summary>
    public int Submitted { get; init; }

    /// <summary>本次命中的风险案例数（新增 + 累加）</summary>
    public int Hits { get; init; }

    /// <summary>命中案例列表</summary>
    public List<RiskCaseResponse> Cases { get; init; } = [];

    /// <summary>是否需要拦截（存在 Block 级未处置命中）</summary>
    public bool Blocked => Cases.Any(c => c.Disposition == RiskDisposition.Block && c.Status is RiskCaseStatus.Open or RiskCaseStatus.Reviewing);
}

/// <summary>风控决策请求（内部接口，业务方下单/领券前调用）</summary>
public sealed record RiskDecisionRequest
{
    /// <summary>场景编码</summary>
    [Required, MaxLength(50)]
    public string Scene { get; init; } = string.Empty;

    /// <summary>用户 ID（可选）</summary>
    public Guid? UserId { get; init; }

    /// <summary>商户 ID（可选）</summary>
    public Guid? MerchantId { get; init; }

    /// <summary>客户端 IP（可选）</summary>
    [MaxLength(64)]
    public string? Ip { get; init; }

    /// <summary>设备 ID（可选）</summary>
    [MaxLength(128)]
    public string? DeviceId { get; init; }
}

/// <summary>风控决策响应 — 是否放行及原因</summary>
public sealed record RiskDecisionResponse
{
    /// <summary>是否放行（true=放行，false=拦截）</summary>
    public bool Allow { get; init; }

    /// <summary>决策理由（拦截时说明原因）</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>命中的黑名单 ID（如有）</summary>
    public Guid? BlacklistId { get; init; }

    /// <summary>命中的未处置案例 ID（如有）</summary>
    public Guid? CaseId { get; init; }

    /// <summary>处置级别（观察/拦截，仅拦截时有意义）</summary>
    public RiskDisposition? Disposition { get; init; }
}

/// <summary>风控规则保存请求（平台端）</summary>
public sealed record SaveRiskRuleRequest
{
    /// <summary>规则名称</summary>
    [Required, MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    /// <summary>场景编码</summary>
    [Required, MaxLength(50)]
    public string Scene { get; init; } = string.Empty;

    /// <summary>统计维度</summary>
    public RiskDimension Dimension { get; init; }

    /// <summary>时间窗口（秒，1-86400）</summary>
    [Range(1, 86400)]
    public int WindowSeconds { get; init; }

    /// <summary>窗口内命中阈值（1-100000）</summary>
    [Range(1, 100000)]
    public int Threshold { get; init; }

    /// <summary>处置级别</summary>
    public RiskDisposition Disposition { get; init; }

    /// <summary>商户 ID（null = 全局规则）</summary>
    public Guid? MerchantId { get; init; }

    /// <summary>规则说明</summary>
    [MaxLength(500)]
    public string? Description { get; init; }
}

/// <summary>风控规则响应</summary>
public sealed record RiskRuleResponse
{
    /// <summary>规则 ID</summary>
    public Guid Id { get; init; }

    /// <summary>规则名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>场景编码</summary>
    public string Scene { get; init; } = string.Empty;

    /// <summary>统计维度</summary>
    public RiskDimension Dimension { get; init; }

    /// <summary>时间窗口（秒）</summary>
    public int WindowSeconds { get; init; }

    /// <summary>窗口内命中阈值</summary>
    public int Threshold { get; init; }

    /// <summary>处置级别</summary>
    public RiskDisposition Disposition { get; init; }

    /// <summary>商户 ID（null = 全局）</summary>
    public Guid? MerchantId { get; init; }

    /// <summary>规则说明</summary>
    public string? Description { get; init; }

    /// <summary>是否启用</summary>
    public bool Enabled { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>风险案例响应</summary>
public sealed record RiskCaseResponse
{
    /// <summary>案例 ID</summary>
    public Guid Id { get; init; }

    /// <summary>命中的规则 ID（黑名单拦截为 null）</summary>
    public Guid? RuleId { get; init; }

    /// <summary>规则名称（快照）</summary>
    public string RuleName { get; init; } = string.Empty;

    /// <summary>场景编码</summary>
    public string Scene { get; init; } = string.Empty;

    /// <summary>统计维度</summary>
    public RiskDimension Dimension { get; init; }

    /// <summary>命中维度键</summary>
    public string DimensionKey { get; init; } = string.Empty;

    /// <summary>用户 ID</summary>
    public Guid? UserId { get; init; }

    /// <summary>商户 ID</summary>
    public Guid? MerchantId { get; init; }

    /// <summary>客户端 IP</summary>
    public string? Ip { get; init; }

    /// <summary>设备 ID</summary>
    public string? DeviceId { get; init; }

    /// <summary>窗口内发生次数</summary>
    public int OccurredCount { get; init; }

    /// <summary>规则阈值（快照）</summary>
    public int Threshold { get; init; }

    /// <summary>处置级别</summary>
    public RiskDisposition Disposition { get; init; }

    /// <summary>来源（RULE_HIT / BLACKLIST）</summary>
    public string Source { get; init; } = "RULE_HIT";

    /// <summary>风险摘要</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>状态</summary>
    public RiskCaseStatus Status { get; init; }

    /// <summary>处置备注</summary>
    public string? ResolutionNote { get; init; }

    /// <summary>处置时间</summary>
    public DateTime? ResolvedAt { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>风险案例处置请求（平台端）</summary>
public sealed record ResolveRiskCaseRequest
{
    /// <summary>处置备注</summary>
    [MaxLength(500)]
    public string? Note { get; init; }
}

/// <summary>黑名单保存请求（平台端）</summary>
public sealed record SaveBlacklistRequest
{
    /// <summary>对象类型（用户/IP/设备）</summary>
    public BlacklistTargetType TargetType { get; init; }

    /// <summary>对象值（用户 ID 字符串 / IP / 设备 ID）</summary>
    [Required, MaxLength(128)]
    public string TargetValue { get; init; } = string.Empty;

    /// <summary>拉黑原因</summary>
    [Required, MaxLength(500)]
    public string Reason { get; init; } = string.Empty;

    /// <summary>过期时间（UTC，null = 永久）</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>商户 ID（null = 平台全局）</summary>
    public Guid? MerchantId { get; init; }
}

/// <summary>黑名单响应</summary>
public sealed record BlacklistResponse
{
    /// <summary>条目 ID</summary>
    public Guid Id { get; init; }

    /// <summary>对象类型</summary>
    public BlacklistTargetType TargetType { get; init; }

    /// <summary>对象值</summary>
    public string TargetValue { get; init; } = string.Empty;

    /// <summary>拉黑原因</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>过期时间（null = 永久）</summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>商户 ID（null = 平台全局）</summary>
    public Guid? MerchantId { get; init; }

    /// <summary>是否启用</summary>
    public bool Enabled { get; init; }

    /// <summary>是否已过期</summary>
    public bool Expired { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>风控事件查询响应（平台端）</summary>
public sealed record RiskEventResponse
{
    /// <summary>事件 ID</summary>
    public Guid Id { get; init; }

    /// <summary>场景编码</summary>
    public string Scene { get; init; } = string.Empty;

    /// <summary>用户 ID</summary>
    public Guid? UserId { get; init; }

    /// <summary>商户 ID</summary>
    public Guid? MerchantId { get; init; }

    /// <summary>客户端 IP</summary>
    public string? Ip { get; init; }

    /// <summary>设备 ID</summary>
    public string? DeviceId { get; init; }

    /// <summary>附加载荷 JSON</summary>
    public string? PayloadJson { get; init; }

    /// <summary>事件发生时间</summary>
    public DateTime OccurredAt { get; init; }
}

/// <summary>风控概览响应（平台端）</summary>
public sealed record RiskOverviewResponse
{
    /// <summary>启用规则数</summary>
    public int EnabledRuleCount { get; init; }

    /// <summary>规则总数</summary>
    public int TotalRuleCount { get; init; }

    /// <summary>黑名单条目数</summary>
    public int BlacklistCount { get; init; }

    /// <summary>待处置案例数（Open）</summary>
    public int OpenCaseCount { get; init; }

    /// <summary>复核中案例数（Reviewing）</summary>
    public int ReviewingCaseCount { get; init; }

    /// <summary>今日上报事件数</summary>
    public int TodayEventCount { get; init; }

    /// <summary>今日命中案例数</summary>
    public int TodayHitCount { get; init; }
}

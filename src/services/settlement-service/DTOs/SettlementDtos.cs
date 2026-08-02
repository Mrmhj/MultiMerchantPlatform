using System.ComponentModel.DataAnnotations;
using SettlementService.Domain.Enums;

namespace SettlementService.DTOs;

/// <summary>生成结算单请求（平台端）</summary>
public sealed record GenerateSettlementRequest
{
    /// <summary>周期开始（UTC，可选，默认不限）</summary>
    public DateTime? CycleStart { get; init; }

    /// <summary>周期结束（UTC，可选，默认不限）</summary>
    public DateTime? CycleEnd { get; init; }
}

/// <summary>佣金规则设置请求（平台端）</summary>
public sealed record SaveCommissionRuleRequest
{
    /// <summary>佣金比例（0-100 百分数）</summary>
    [Range(0, 100)]
    public decimal Rate { get; init; }
}

/// <summary>佣金规则响应</summary>
public sealed record CommissionRuleResponse
{
    /// <summary>商户 ID</summary>
    public Guid MerchantId { get; init; }

    /// <summary>佣金比例（百分数）</summary>
    public decimal Rate { get; init; }

    /// <summary>是否使用平台默认比例（未配置时为 true）</summary>
    public bool IsDefault { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>结算明细响应</summary>
public sealed record SettlementItemResponse
{
    /// <summary>子订单 ID</summary>
    public Guid SubOrderId { get; init; }

    /// <summary>订单号</summary>
    public string OrderNo { get; init; } = string.Empty;

    /// <summary>商品金额（元）</summary>
    public decimal ProductAmount { get; init; }

    /// <summary>佣金金额（元）</summary>
    public decimal CommissionAmount { get; init; }

    /// <summary>结算金额（元）</summary>
    public decimal SettleAmount { get; init; }
}

/// <summary>结算单响应（列表不含明细）</summary>
public sealed record SettlementResponse
{
    /// <summary>结算单 ID</summary>
    public Guid Id { get; init; }

    /// <summary>商户 ID</summary>
    public Guid MerchantId { get; init; }

    /// <summary>商户名称（快照）</summary>
    public string MerchantName { get; init; } = string.Empty;

    /// <summary>周期开始（UTC）</summary>
    public DateTime CycleStart { get; init; }

    /// <summary>周期结束（UTC）</summary>
    public DateTime CycleEnd { get; init; }

    /// <summary>订单总金额（元）</summary>
    public decimal TotalOrderAmount { get; init; }

    /// <summary>佣金总额（元）</summary>
    public decimal TotalCommission { get; init; }

    /// <summary>结算金额（元）</summary>
    public decimal SettlementAmount { get; init; }

    /// <summary>状态（Pending/Settled/Paid）</summary>
    public SettlementStatus Status { get; init; }

    /// <summary>确认结算时间（未结算为 null）</summary>
    public DateTime? SettledAt { get; init; }

    /// <summary>打款时间（未打款为 null）</summary>
    public DateTime? PaidAt { get; init; }

    /// <summary>明细列表（详情接口含，列表接口为空）</summary>
    public List<SettlementItemResponse> Items { get; init; } = [];

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>商户结算概览响应</summary>
public sealed record MerchantSettlementSummaryResponse
{
    /// <summary>待结算单数</summary>
    public int PendingCount { get; init; }

    /// <summary>已结算单数</summary>
    public int SettledCount { get; init; }

    /// <summary>已打款单数</summary>
    public int PaidCount { get; init; }

    /// <summary>待结算金额（元）</summary>
    public decimal PendingAmount { get; init; }

    /// <summary>累计结算金额（已结算 + 已打款，元）</summary>
    public decimal SettledAmount { get; init; }

    /// <summary>累计佣金（元）</summary>
    public decimal TotalCommission { get; init; }
}

/// <summary>生成结算单响应（平台端）</summary>
public sealed record GenerateSettlementResponse
{
    /// <summary>生成的结算单列表</summary>
    public List<SettlementResponse> Settlements { get; init; } = [];

    /// <summary>被跳过的子订单数量（已结算过或失败，防重复）</summary>
    public int SkippedCount { get; init; }
}

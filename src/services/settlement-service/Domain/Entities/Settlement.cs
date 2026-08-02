using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;
using SettlementService.Domain.Enums;

namespace SettlementService.Domain.Entities;

/// <summary>
/// 结算单 — 按商户 + 结算周期聚合已完成子订单的佣金与结算金额。
/// 多租户：商户维度（MerchantId）隔离。
/// 状态机：Pending（待结算）→ Settled（已结算）→ Paid（已打款）。
/// </summary>
public sealed class Settlement : MultiTenantEntity
{
    private readonly List<SettlementItem> _items = [];

    private Settlement() { } // EF Core

    /// <summary>创建结算单（初始 Pending）</summary>
    /// <param name="merchantId">商户 ID</param>
    /// <param name="merchantName">商户名称（快照）</param>
    /// <param name="cycleStart">结算周期开始（UTC）</param>
    /// <param name="cycleEnd">结算周期结束（UTC）</param>
    /// <param name="now">创建时间（UTC）</param>
    [SetsRequiredMembers]
    public Settlement(Guid merchantId, string merchantName, DateTime cycleStart, DateTime cycleEnd, DateTime now)
    {
        if (cycleEnd < cycleStart)
            throw new DomainException("结算周期结束时间不能早于开始时间", "INVALID_CYCLE");

        MerchantId = merchantId;
        MerchantName = (merchantName ?? string.Empty).Trim();
        CycleStart = cycleStart;
        CycleEnd = cycleEnd;
        Status = SettlementStatus.Pending;
        CreatedAt = now;
    }

    /// <summary>商户名称（快照）</summary>
    public string MerchantName { get; private set; } = string.Empty;

    /// <summary>结算周期开始（UTC）</summary>
    public DateTime CycleStart { get; private set; }

    /// <summary>结算周期结束（UTC）</summary>
    public DateTime CycleEnd { get; private set; }

    /// <summary>订单总金额（明细商品金额合计）</summary>
    public decimal TotalOrderAmount { get; private set; }

    /// <summary>佣金总额（按佣金规则计算）</summary>
    public decimal TotalCommission { get; private set; }

    /// <summary>结算金额（订单总金额 - 佣金总额）</summary>
    public decimal SettlementAmount => TotalOrderAmount - TotalCommission;

    /// <summary>状态（Pending/Settled/Paid）</summary>
    public SettlementStatus Status { get; private set; }

    /// <summary>确认结算时间（未结算为 null）</summary>
    public DateTime? SettledAt { get; private set; }

    /// <summary>打款时间（未打款为 null）</summary>
    public DateTime? PaidAt { get; private set; }

    /// <summary>结算明细列表</summary>
    public IReadOnlyList<SettlementItem> Items => _items;

    /// <summary>添加结算明细（仅生成结算单时调用，累加金额）</summary>
    /// <param name="subOrderId">子订单 ID（唯一，防重复结算）</param>
    /// <param name="orderNo">订单号（快照）</param>
    /// <param name="productAmount">商品金额</param>
    /// <param name="commissionRate">佣金比例（百分数）</param>
    /// <returns>新增的明细</returns>
    public SettlementItem AddItem(Guid subOrderId, string orderNo, decimal productAmount, decimal commissionRate)
    {
        if (Status != SettlementStatus.Pending)
            throw new DomainException("结算单已确认，不能追加明细", "SETTLEMENT_NOT_PENDING");

        var commission = Math.Round(productAmount * commissionRate / 100m, 2, MidpointRounding.AwayFromZero);
        var item = new SettlementItem(Id, subOrderId, orderNo, productAmount, commission);

        _items.Add(item);
        TotalOrderAmount += productAmount;
        TotalCommission += commission;
        return item;
    }

    /// <summary>确认结算（Pending → Settled）</summary>
    /// <param name="now">确认时间（UTC）</param>
    public void Settle(DateTime now)
    {
        if (Status != SettlementStatus.Pending)
            throw new DomainException($"当前状态不允许确认结算（{Status}）", "SETTLEMENT_STATE_INVALID");
        if (_items.Count == 0)
            throw new DomainException("结算单无明细，不能确认", "SETTLEMENT_EMPTY");

        Status = SettlementStatus.Settled;
        SettledAt = now;
    }

    /// <summary>标记已打款（Settled → Paid）</summary>
    /// <param name="now">打款时间（UTC）</param>
    public void MarkPaid(DateTime now)
    {
        if (Status != SettlementStatus.Settled)
            throw new DomainException($"当前状态不允许打款（{Status}）", "SETTLEMENT_STATE_INVALID");

        Status = SettlementStatus.Paid;
        PaidAt = now;
    }
}

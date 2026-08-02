using BuildingBlocks.Core.Entities;

namespace SettlementService.Domain.Entities;

/// <summary>
/// 结算明细 — 结算单中的子订单条目（订单金额 / 佣金 / 结算金额快照）。
/// 一个子订单仅可进入一张结算单（SubOrderId 唯一索引，防重复结算）。
/// </summary>
public sealed class SettlementItem : Entity
{
    private SettlementItem() { } // EF Core

    /// <summary>创建结算明细</summary>
    /// <param name="settlementId">结算单 ID</param>
    /// <param name="subOrderId">子订单 ID（唯一）</param>
    /// <param name="orderNo">订单号（快照）</param>
    /// <param name="productAmount">商品金额</param>
    /// <param name="commissionAmount">佣金金额</param>
    public SettlementItem(Guid settlementId, Guid subOrderId, string orderNo,
        decimal productAmount, decimal commissionAmount)
    {
        SettlementId = settlementId;
        SubOrderId = subOrderId;
        OrderNo = (orderNo ?? string.Empty).Trim();
        ProductAmount = productAmount;
        CommissionAmount = commissionAmount;
    }

    /// <summary>所属结算单 ID</summary>
    public Guid SettlementId { get; private set; }

    /// <summary>子订单 ID（唯一，防重复结算）</summary>
    public Guid SubOrderId { get; private set; }

    /// <summary>订单号（快照）</summary>
    public string OrderNo { get; private set; } = string.Empty;

    /// <summary>商品金额（元）</summary>
    public decimal ProductAmount { get; private set; }

    /// <summary>佣金金额（元）</summary>
    public decimal CommissionAmount { get; private set; }

    /// <summary>结算金额（商品金额 - 佣金，元）</summary>
    public decimal SettleAmount => ProductAmount - CommissionAmount;
}

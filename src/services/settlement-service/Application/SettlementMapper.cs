using SettlementService.Domain.Entities;
using SettlementService.DTOs;

namespace SettlementService.Application;

/// <summary>
/// 实体 → DTO 映射。
/// </summary>
public static class SettlementMapper
{
    /// <summary>结算单实体转响应 DTO</summary>
    /// <param name="settlement">结算单实体</param>
    /// <param name="includeItems">是否包含明细</param>
    /// <returns>结算单响应</returns>
    public static SettlementResponse ToResponse(Settlement settlement, bool includeItems) => new()
    {
        Id = settlement.Id,
        MerchantId = settlement.MerchantId,
        MerchantName = settlement.MerchantName,
        CycleStart = settlement.CycleStart,
        CycleEnd = settlement.CycleEnd,
        TotalOrderAmount = settlement.TotalOrderAmount,
        TotalCommission = settlement.TotalCommission,
        SettlementAmount = settlement.SettlementAmount,
        Status = settlement.Status,
        SettledAt = settlement.SettledAt,
        PaidAt = settlement.PaidAt,
        Items = includeItems
            ? settlement.Items.Select(ToItemResponse).ToList()
            : [],
        CreatedAt = settlement.CreatedAt,
    };

    /// <summary>结算明细实体转响应 DTO</summary>
    /// <param name="item">结算明细实体</param>
    /// <returns>明细响应</returns>
    public static SettlementItemResponse ToItemResponse(SettlementItem item) => new()
    {
        SubOrderId = item.SubOrderId,
        OrderNo = item.OrderNo,
        ProductAmount = item.ProductAmount,
        CommissionAmount = item.CommissionAmount,
        SettleAmount = item.SettleAmount,
    };

    /// <summary>佣金规则实体转响应 DTO</summary>
    /// <param name="rule">佣金规则实体</param>
    /// <param name="isDefault">是否使用平台默认</param>
    /// <returns>佣金规则响应</returns>
    public static CommissionRuleResponse ToCommissionRuleResponse(CommissionRule rule, bool isDefault) => new()
    {
        MerchantId = rule.MerchantId,
        Rate = rule.Rate,
        IsDefault = isDefault,
        CreatedAt = rule.CreatedAt,
    };
}

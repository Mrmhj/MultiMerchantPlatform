using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;
using System.Diagnostics.CodeAnalysis;

namespace SettlementService.Domain.Entities;

/// <summary>
/// 佣金规则 — 商户的佣金比例（百分数，如 5 = 5%），一个商户一条规则（MerchantId 唯一）。
/// 未配置规则的商户生成结算单时使用平台默认佣金比例（DefaultCommissionRate 配置）。
/// </summary>
public sealed class CommissionRule : MultiTenantEntity
{
    private CommissionRule() { } // EF Core

    /// <summary>创建佣金规则</summary>
    /// <param name="merchantId">商户 ID</param>
    /// <param name="rate">佣金比例（0-100 百分数）</param>
    [SetsRequiredMembers]
    public CommissionRule(Guid merchantId, decimal rate)
    {
        MerchantId = merchantId;
        ChangeRate(rate);
    }

    /// <summary>佣金比例（百分数，0-100）</summary>
    public decimal Rate { get; private set; }

    /// <summary>修改佣金比例</summary>
    /// <param name="rate">佣金比例（0-100 百分数）</param>
    public void ChangeRate(decimal rate)
    {
        if (rate is < 0 or > 100)
            throw new DomainException("佣金比例需在 0-100 之间", "INVALID_COMMISSION_RATE");
        Rate = Math.Round(rate, 2, MidpointRounding.AwayFromZero);
    }
}

namespace MerchantService.Domain.Enums;

/// <summary>
/// 商户状态（入驻审核状态机）。
/// </summary>
public enum MerchantStatus
{
    /// <summary>待审核（入驻申请已提交）</summary>
    Pending = 1,

    /// <summary>已通过（可上架商品、正常营业）</summary>
    Approved = 2,

    /// <summary>已驳回（含驳回原因）</summary>
    Rejected = 3,

    /// <summary>已禁用（平台处罚 / 违规）</summary>
    Disabled = 4
}

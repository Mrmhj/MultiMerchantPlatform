namespace ImService.Domain.Enums;

/// <summary>
/// 会话成员角色：买家 / 商户客服 / 平台管理员 / 系统。
/// </summary>
public enum ChatMemberRole
{
    /// <summary>买家（C 端用户）</summary>
    Buyer = 1,

    /// <summary>商户客服 / 商户员工</summary>
    MerchantStaff = 2,

    /// <summary>平台管理员</summary>
    Admin = 3,

    /// <summary>系统（自动通知，如订单状态）</summary>
    System = 4,
}

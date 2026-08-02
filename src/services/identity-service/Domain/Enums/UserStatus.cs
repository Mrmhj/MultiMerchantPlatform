namespace IdentityService.Domain.Enums;

/// <summary>
/// 用户状态。
/// </summary>
public enum UserStatus
{
    /// <summary>正常可用</summary>
    Active = 1,

    /// <summary>已禁用（管理员操作）</summary>
    Disabled = 2,

    /// <summary>登录失败锁定（临时）</summary>
    Locked = 3
}

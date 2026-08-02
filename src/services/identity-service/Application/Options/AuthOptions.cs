namespace IdentityService.Application.Options;

/// <summary>
/// 认证配置（登录失败锁定策略）。
/// </summary>
public sealed class AuthOptions
{
    /// <summary>配置节名称（appsettings.json 的 Auth 节点）</summary>
    public const string SectionName = "Auth";

    /// <summary>允许的最大连续登录失败次数（达到即锁定）</summary>
    public int MaxFailedLoginAttempts { get; set; } = 5;

    /// <summary>锁定时长（分钟）</summary>
    public int LockoutMinutes { get; set; } = 15;
}

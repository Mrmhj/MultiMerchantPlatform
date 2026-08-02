using System.Text.Json;
using BuildingBlocks.Core.Entities;
using IdentityService.Domain.Enums;

namespace IdentityService.Domain.Entities;

/// <summary>
/// 用户实体 — 平台 C 端用户（买家 / 普通账号）。
/// 状态与行为内聚：注册、登录校验、失败锁定、禁用/启用均由实体方法维护（充血模型）。
/// </summary>
public sealed class User : Entity
{
    private const string DefaultRolesJson = "[\"customer\"]";

    private User() { } // EF Core

    /// <summary>创建用户（初始状态 Active，默认角色 customer）</summary>
    /// <param name="email">登录邮箱（唯一）</param>
    /// <param name="passwordHash">密码哈希（PasswordHasher.Hash 输出）</param>
    /// <param name="displayName">显示名称</param>
    /// <param name="roles">初始角色（默认 customer）</param>
    public User(string email, string passwordHash, string displayName, string[]? roles = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Email = email.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        DisplayName = displayName.Trim();
        RolesJson = roles is { Length: > 0 } ? JsonSerializer.Serialize(roles) : DefaultRolesJson;
        Status = UserStatus.Active;
        FailedLoginCount = 0;
    }

    /// <summary>登录邮箱（唯一，小写存储）</summary>
    public string Email { get; private set; } = null!;

    /// <summary>密码哈希（PBKDF2）</summary>
    public string PasswordHash { get; private set; } = null!;

    /// <summary>显示名称</summary>
    public string DisplayName { get; private set; } = null!;

    /// <summary>用户状态（Active / Disabled / Locked）</summary>
    public UserStatus Status { get; private set; }

    /// <summary>角色集合（JSON 数组存储，如 ["customer","merchant"]）</summary>
    public string RolesJson { get; private set; } = null!;

    /// <summary>连续登录失败次数（达到上限触发锁定）</summary>
    public int FailedLoginCount { get; private set; }

    /// <summary>锁定截止时间（过期自动解锁）</summary>
    public DateTime? LockoutEndTime { get; private set; }

    /// <summary>最近一次成功登录时间</summary>
    public DateTime? LastLoginAt { get; private set; }

    /// <summary>角色集合（反序列化视图）</summary>
    public string[] Roles => JsonSerializer.Deserialize<string[]>(RolesJson) ?? [];

    /// <summary>是否当前处于锁定状态（含未过期锁定）</summary>
    /// <param name="timeProvider">时间提供器（可测试）</param>
    /// <returns>true 表示已锁定</returns>
    public bool IsLocked(TimeProvider timeProvider)
    {
        if (Status == UserStatus.Disabled)
            return true;
        if (LockoutEndTime.HasValue && LockoutEndTime > timeProvider.GetUtcNow().UtcDateTime)
            return true;
        if (Status == UserStatus.Locked && (!LockoutEndTime.HasValue || LockoutEndTime <= timeProvider.GetUtcNow().UtcDateTime))
            return false; // 锁定已过期，由登录流程恢复状态
        return Status == UserStatus.Locked;
    }

    /// <summary>是否可登录（未禁用且未锁定）</summary>
    /// <param name="timeProvider">时间提供器</param>
    /// <returns>true 表示允许尝试登录</returns>
    public bool CanLogin(TimeProvider timeProvider)
        => Status != UserStatus.Disabled && !IsLocked(timeProvider);

    /// <summary>登录成功 — 重置失败计数、记录登录时间、恢复 Active 状态</summary>
    /// <param name="timeProvider">时间提供器</param>
    public void MarkLoginSuccess(TimeProvider timeProvider)
    {
        Status = UserStatus.Active;
        FailedLoginCount = 0;
        LockoutEndTime = null;
        LastLoginAt = timeProvider.GetUtcNow().UtcDateTime;
    }

    /// <summary>登录失败 — 失败计数 +1，达到上限则锁定（指数锁定时间）</summary>
    /// <param name="timeProvider">时间提供器</param>
    /// <param name="maxAttempts">允许的最大失败次数</param>
    /// <param name="lockoutDuration">锁定时长</param>
    /// <returns>是否已触发锁定</returns>
    public bool MarkLoginFailed(TimeProvider timeProvider, int maxAttempts, TimeSpan lockoutDuration)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= maxAttempts)
        {
            Status = UserStatus.Locked;
            LockoutEndTime = timeProvider.GetUtcNow().UtcDateTime.Add(lockoutDuration);
            return true;
        }
        return false;
    }

    /// <summary>手动解锁（管理员）</summary>
    public void Unlock()
    {
        Status = UserStatus.Active;
        FailedLoginCount = 0;
        LockoutEndTime = null;
    }

    /// <summary>禁用用户（管理员）</summary>
    public void Disable() => Status = UserStatus.Disabled;

    /// <summary>启用用户（管理员）</summary>
    public void Enable()
    {
        Status = UserStatus.Active;
        LockoutEndTime = null;
    }

    /// <summary>更新显示名称</summary>
    /// <param name="displayName">新的显示名称</param>
    public void UpdateProfile(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName.Trim();
    }

    /// <summary>重置密码（管理员或忘记密码流程）</summary>
    /// <param name="newPasswordHash">新密码哈希</param>
    public void ResetPassword(string newPasswordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash);
        PasswordHash = newPasswordHash;
        FailedLoginCount = 0;
        LockoutEndTime = null;
        Status = UserStatus.Active;
    }
}

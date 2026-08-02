using System.ComponentModel.DataAnnotations;

namespace IdentityService.DTOs;

/// <summary>注册请求</summary>
public sealed record RegisterRequest
{
    /// <summary>登录邮箱（唯一，注册后即登录）</summary>
    [Required, EmailAddress, StringLength(200)]
    public required string Email { get; init; }

    /// <summary>密码（至少 6 位）</summary>
    [Required, StringLength(100, MinimumLength = 6)]
    public required string Password { get; init; }

    /// <summary>显示名称（可选，缺省用邮箱前缀）</summary>
    [StringLength(100)]
    public string? DisplayName { get; init; }
}

/// <summary>登录请求</summary>
public sealed record LoginRequest
{
    /// <summary>登录邮箱</summary>
    [Required, EmailAddress, StringLength(200)]
    public required string Email { get; init; }

    /// <summary>密码</summary>
    [Required, StringLength(100, MinimumLength = 1)]
    public required string Password { get; init; }
}

/// <summary>认证响应（注册/登录成功返回）</summary>
public sealed record AuthResponse
{
    /// <summary>JWT 访问令牌（请求头 Authorization: Bearer &lt;token&gt;）</summary>
    public required string Token { get; init; }

    /// <summary>令牌过期时间（UTC）</summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>用户信息</summary>
    public required UserResponse User { get; init; }
}

/// <summary>用户信息响应</summary>
public sealed record UserResponse
{
    /// <summary>用户 ID</summary>
    public Guid Id { get; init; }

    /// <summary>登录邮箱</summary>
    public required string Email { get; init; }

    /// <summary>显示名称</summary>
    public required string DisplayName { get; init; }

    /// <summary>角色集合</summary>
    public required string[] Roles { get; init; }

    /// <summary>用户状态（Active / Disabled / Locked）</summary>
    public int Status { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>最近一次登录时间</summary>
    public DateTime? LastLoginAt { get; init; }
}

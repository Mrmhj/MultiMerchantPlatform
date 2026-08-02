using System.Security.Claims;
using BuildingBlocks.Security;
using IdentityService.Domain.Entities;
using IdentityService.DTOs;
using Microsoft.AspNetCore.Http;

namespace IdentityService.Application;

/// <summary>
/// 当前用户访问器 — 从 HttpContext 的 JWT Claims 解析当前登录用户（ICurrentUser 实现）。
/// </summary>
public sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    /// <inheritdoc />
    public Guid UserId
        => Guid.TryParse(User?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : Guid.Empty;

    /// <inheritdoc />
    public string UserName
        => User?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName) ?? string.Empty;

    /// <inheritdoc />
    public Guid? MerchantId
        => Guid.TryParse(User?.FindFirstValue("merchant_id"), out var m) ? m : null;

    /// <inheritdoc />
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public string[] Roles
        => User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];
}

/// <summary>
/// 实体 → DTO 映射（手写映射，避免反射开销）。
/// </summary>
public static class UserMapper
{
    /// <summary>用户实体转响应 DTO</summary>
    /// <param name="user">用户实体</param>
    /// <returns>用户信息响应</returns>
    public static UserResponse ToResponse(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        Roles = user.Roles,
        Status = (int)user.Status,
        CreatedAt = user.CreatedAt,
        LastLoginAt = user.LastLoginAt,
    };

    /// <summary>组装认证响应（用户 + JWT）</summary>
    /// <param name="user">用户实体</param>
    /// <param name="token">JWT 令牌</param>
    /// <param name="expiresAt">令牌过期时间</param>
    /// <returns>认证响应</returns>
    public static AuthResponse ToAuthResponse(User user, string token, DateTime expiresAt) => new()
    {
        Token = token,
        ExpiresAt = expiresAt,
        User = ToResponse(user),
    };
}

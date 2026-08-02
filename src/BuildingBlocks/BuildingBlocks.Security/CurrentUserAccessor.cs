using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Security;

/// <summary>
/// 当前用户访问器 — 从 HttpContext 的 JWT Claims 解析当前登录用户（ICurrentUser 实现）。
/// 适用于所有 Web API 服务（配合 JwtBearer + MapInboundClaims=false）。
/// </summary>
public sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    /// <inheritdoc />
    public Guid UserId
        => Guid.TryParse(User?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : Guid.Empty;

    /// <inheritdoc />
    public string UserName
        => User?.FindFirstValue(JwtRegisteredClaimNames.UniqueName) ?? string.Empty;

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
/// 当前用户依赖注入注册。
/// </summary>
public static class CurrentUserServiceCollectionExtensions
{
    /// <summary>注册 ICurrentUser（需先 AddHttpContextAccessor）</summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddCurrentUser(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUserAccessor>();
        return services;
    }
}

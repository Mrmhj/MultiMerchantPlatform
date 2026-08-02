using BuildingBlocks.MultiTenant;
using Microsoft.AspNetCore.Http;

namespace ImService.Infrastructure;

/// <summary>
/// 商户提供者 — 从请求头 X-Merchant-Id 解析当前商户 ID（多租户隔离入口）。
/// 商户端接口（Web/网关）在请求头携带当前商户 ID；买家接口 / 内部接口（可不带）读全量。
/// </summary>
public sealed class HttpMerchantProvider(IHttpContextAccessor httpContextAccessor) : ITenantProvider
{
    private const string HeaderName = "X-Merchant-Id";

    /// <inheritdoc />
    public Guid? CurrentMerchantId
        => Guid.TryParse(httpContextAccessor.HttpContext?.Request.Headers[HeaderName].FirstOrDefault(),
            out var id) ? id : null;

    /// <inheritdoc />
    public Guid? CurrentUserId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            var sub = user?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    /// <inheritdoc />
    public bool IsPlatformAdmin
        => httpContextAccessor.HttpContext?.User?.IsInRole("admin") ?? false;
}

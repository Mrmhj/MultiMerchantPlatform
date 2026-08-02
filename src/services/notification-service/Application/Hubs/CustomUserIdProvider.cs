using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.SignalR;

namespace NotificationService.Application.Hubs;

/// <summary>
/// 自定义用户标识提供者 — 从 JWT Claims 提取用户 ID（sub）作为 SignalR UserIdentifier。
/// 服务端 JwtBearer 配置了 MapInboundClaims=false，sub claim 保留原名，需显式读取。
/// </summary>
public sealed class CustomUserIdProvider : IUserIdProvider
{
    /// <inheritdoc />
    public string? GetUserId(HubConnectionContext connection)
    {
        var claim = connection.User?.FindFirst(JwtRegisteredClaimNames.Sub);
        return string.IsNullOrWhiteSpace(claim?.Value) ? null : claim.Value;
    }
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Security;

/// <summary>
/// JWT Token 生成器。
/// </summary>
public class JwtTokenService(JwtOptions options)
{
    private readonly JwtOptions _options = options;

    public string GenerateToken(Guid userId, string userName, string[] roles, Guid? merchantId = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, userName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        if (merchantId.HasValue)
            claims.Add(new Claim("merchant_id", merchantId.Value.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>
/// 当前用户上下文接口 — 从 JWT Claims 中解析当前用户信息。
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    string UserName { get; }
    Guid? MerchantId { get; }
    bool IsAuthenticated { get; }
    string[] Roles { get; }
}

/// <summary>
/// JWT 配置选项。
/// </summary>
public record JwtOptions
{
    /// <summary>JWT 签名密钥（至少 32 字符；生产必须通过配置注入，勿用默认值）</summary>
    public string SecretKey { get; init; } = "MultiMerchantPlatform_DefaultSecretKey_2026_Min32Chars!";
    public string Issuer { get; init; } = "MultiMerchantPlatform";
    public string Audience { get; init; } = "MultiMerchantPlatform Clients";
    public int ExpiryMinutes { get; init; } = 120;
}

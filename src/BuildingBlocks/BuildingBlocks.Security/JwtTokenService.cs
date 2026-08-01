using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Security;

/// <summary>
/// JWT Token 生成器。
/// </summary>
public class JwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(JwtOptions options)
    {
        _options = options;
    }

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
/// 当前用户上下文接口。
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    string UserName { get; }
    Guid? MerchantId { get; }
    bool IsAuthenticated { get; }
    string[] Roles { get; }
}

public class JwtOptions
{
    public string SecretKey { get; set; } = "MultiMerchantPlatform_DefaultSecretKey_2026_Min32Chars!";
    public string Issuer { get; set; } = "MultiMerchantPlatform";
    public string Audience { get; set; } = "MultiMerchantPlatform Clients";
    public int ExpiryMinutes { get; set; } = 120;
}

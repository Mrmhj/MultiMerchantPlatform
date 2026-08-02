using System.Security.Cryptography;

namespace IdentityService.Domain;

/// <summary>
/// 密码哈希器 — PBKDF2-SHA256（.NET 内置，无第三方依赖）。
/// 存储格式：<c>迭代次数.盐(Base64).哈希(Base64)</c>，如 <c>10000.aGVsbG8=.xxxx</c>。
/// </summary>
public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    /// <summary>对密码做 PBKDF2 哈希（随机盐）</summary>
    /// <param name="password">明文密码</param>
    /// <returns>哈希字符串（迭代次数.盐.哈希，Base64）</returns>
    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>校验明文密码与存储哈希是否匹配（恒定时间比较防时序攻击）</summary>
    /// <param name="password">明文密码</param>
    /// <param name="storedHash">存储的哈希字符串</param>
    /// <returns>true 表示匹配</returns>
    public static bool Verify(string password, string storedHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(storedHash);

        var parts = storedHash.Split('.');
        if (parts.Length != 3)
            return false;
        if (!int.TryParse(parts[0], out var iterations) || iterations <= 0)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

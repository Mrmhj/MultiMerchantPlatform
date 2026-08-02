using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Security;
using IdentityService.Application.Options;
using IdentityService.Domain;
using IdentityService.DTOs;
using IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Commands;

/// <summary>登录命令 — 校验凭证、失败锁定策略、签发 JWT</summary>
public sealed record LoginCommand(string Email, string Password) : ICommand<AuthResponse>;

/// <summary>登录命令处理器</summary>
public sealed class LoginCommandHandler(
    IdentityDbContext db,
    JwtTokenService jwtTokenService,
    JwtOptions jwtOptions,
    IOptions<AuthOptions> authOptions,
    TimeProvider timeProvider) : ICommandHandler<LoginCommand, AuthResponse>
{
    /// <inheritdoc />
    public async Task<AuthResponse> HandleAsync(LoginCommand command, CancellationToken ct = default)
    {
        var email = command.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        // 统一错误提示，避免泄露账号是否存在
        if (user is null)
            throw new DomainException("邮箱或密码错误", "INVALID_CREDENTIALS");

        if (!user.CanLogin(timeProvider))
            throw new DomainException("账号已锁定，请稍后再试", "ACCOUNT_LOCKED");

        if (!PasswordHasher.Verify(command.Password, user.PasswordHash))
        {
            var lockout = TimeSpan.FromMinutes(authOptions.Value.LockoutMinutes);
            var locked = user.MarkLoginFailed(timeProvider, authOptions.Value.MaxFailedLoginAttempts, lockout);
            await db.SaveChangesAsync(ct);

            throw new DomainException(
                locked ? $"登录失败次数过多，账号已锁定 {authOptions.Value.LockoutMinutes} 分钟" : "邮箱或密码错误",
                locked ? "ACCOUNT_LOCKED" : "INVALID_CREDENTIALS");
        }

        user.MarkLoginSuccess(timeProvider);
        await db.SaveChangesAsync(ct);

        var token = jwtTokenService.GenerateToken(user.Id, user.Email, user.Roles);
        var expiresAt = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(jwtOptions.ExpiryMinutes);
        return UserMapper.ToAuthResponse(user, token, expiresAt);
    }
}

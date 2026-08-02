using IdentityService.Domain;
using IdentityService.Domain.Entities;
using IdentityService.DTOs;
using IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Security;

namespace IdentityService.Application.Commands;

/// <summary>注册命令 — 创建用户并签发 JWT（注册即登录）</summary>
public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string? DisplayName) : ICommand<AuthResponse>;

/// <summary>注册命令处理器</summary>
public sealed class RegisterUserCommandHandler(
    IdentityDbContext db,
    JwtTokenService jwtTokenService,
    JwtOptions jwtOptions,
    TimeProvider timeProvider) : ICommandHandler<RegisterUserCommand, AuthResponse>
{
    /// <inheritdoc />
    public async Task<AuthResponse> HandleAsync(RegisterUserCommand command, CancellationToken ct = default)
    {
        var email = command.Email.Trim().ToLowerInvariant();

        // 邮箱唯一性校验
        var exists = await db.Users.AnyAsync(u => u.Email == email, ct);
        if (exists)
            throw new DomainException("该邮箱已注册", "EMAIL_EXISTS");

        var displayName = string.IsNullOrWhiteSpace(command.DisplayName)
            ? email.Split('@')[0]
            : command.DisplayName.Trim();

        var user = new User(email, PasswordHasher.Hash(command.Password), displayName);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        var token = jwtTokenService.GenerateToken(user.Id, user.Email, user.Roles);
        var expiresAt = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(jwtOptions.ExpiryMinutes);
        return UserMapper.ToAuthResponse(user, token, expiresAt);
    }
}

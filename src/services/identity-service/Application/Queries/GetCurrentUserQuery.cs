using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Security;
using IdentityService.DTOs;
using IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Application.Queries;

/// <summary>当前用户查询 — 从 JWT 中解析用户 ID 并返回用户信息</summary>
public sealed record GetCurrentUserQuery : IQuery<UserResponse>;

/// <summary>当前用户查询处理器</summary>
public sealed class GetCurrentUserQueryHandler(
    IdentityDbContext db,
    ICurrentUser currentUser) : IQueryHandler<GetCurrentUserQuery, UserResponse>
{
    /// <inheritdoc />
    public async Task<UserResponse> HandleAsync(GetCurrentUserQuery query, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty)
            throw new DomainException("未认证或令牌无效", "UNAUTHENTICATED");

        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct);
        if (user is null)
            throw new NotFoundException("用户", currentUser.UserId);

        return UserMapper.ToResponse(user);
    }
}

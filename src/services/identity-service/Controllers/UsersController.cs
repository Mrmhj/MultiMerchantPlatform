using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using IdentityService.Application.Queries;
using IdentityService.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Controllers;

/// <summary>
/// 用户 API — 当前用户信息查询（需 JWT 认证）。
/// </summary>
[ApiController]
[Route("api/users")]
[Produces("application/json")]
public sealed class UsersController(IMediator mediator) : ControllerBase
{
    /// <summary>获取当前登录用户信息（需请求头 Authorization: Bearer &lt;token&gt;）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 用户信息；401 — 未认证或令牌无效；404 — 用户不存在</returns>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.QueryAsync<GetCurrentUserQuery, UserResponse>(new GetCurrentUserQuery(), ct));
        }
        catch (DomainException ex) when (ex.ErrorCode == "UNAUTHENTICATED")
        {
            return Unauthorized(new { error = ex.Message, code = ex.ErrorCode });
        }
        catch (NotFoundException)
        {
            return NotFound("用户不存在");
        }
    }
}

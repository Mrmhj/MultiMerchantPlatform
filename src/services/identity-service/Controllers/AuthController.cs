using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using IdentityService.Application.Commands;
using IdentityService.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Controllers;

/// <summary>
/// 认证 API — 注册 / 登录（JWT 签发）。
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
    /// <summary>注册新用户（邮箱唯一，注册即登录返回 JWT）</summary>
    /// <param name="request">注册请求（邮箱 + 密码 + 显示名）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 认证响应（JWT + 用户信息）；409 — 邮箱已注册</returns>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        try
        {
            var command = new RegisterUserCommand(request.Email, request.Password, request.DisplayName);
            var result = await mediator.SendAsync<RegisterUserCommand, AuthResponse>(command, ct);
            return Created("", result);
        }
        catch (DomainException ex) when (ex.ErrorCode == "EMAIL_EXISTS")
        {
            return Conflict(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>登录（校验凭证，返回 JWT；连续失败触发锁定）</summary>
    /// <param name="request">登录请求（邮箱 + 密码）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 认证响应（JWT + 用户信息）；401 — 凭证错误或账号锁定</returns>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        try
        {
            var command = new LoginCommand(request.Email, request.Password);
            return Ok(await mediator.SendAsync<LoginCommand, AuthResponse>(command, ct));
        }
        catch (DomainException ex)
        {
            return Unauthorized(new { error = ex.Message, code = ex.ErrorCode });
        }
    }
}

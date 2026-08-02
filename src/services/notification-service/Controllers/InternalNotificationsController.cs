using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using NotificationService.Application.Commands;
using NotificationService.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace NotificationService.Controllers;

/// <summary>
/// 通知内部接口（服务间调用）— 站内信 / 短信 / Push 发送，X-Internal-Key 校验。
/// 供 order / logistics / performance / logging / risk 等系统服务接入通知中心。
/// </summary>
[ApiController]
[Route("api/notifications/internal")]
[Produces("application/json")]
public sealed class InternalNotificationsController(
    IMediator mediator,
    IConfiguration configuration) : ControllerBase
{
    private readonly string _internalKey = configuration["Internal:Key"] ?? string.Empty;

    /// <summary>发送站内信（模板渲染或直接内容，可选实时推送）</summary>
    /// <param name="request">发送请求</param>
    /// <param name="key">内部密钥（请求头 X-Internal-Key）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 发送结果；400 — 参数校验失败；401 — 内部密钥无效</returns>
    /// <response code="200">发送成功</response>
    /// <response code="400">参数校验失败</response>
    /// <response code="401">内部密钥无效</response>
    [HttpPost("send")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SendInAppNotificationResponse>> SendInApp(
        [FromBody] SendInAppNotificationRequest request,
        [FromHeader(Name = "X-Internal-Key")] string key,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_internalKey) || key != _internalKey)
            return Unauthorized(new { error = "内部密钥无效" });

        try
        {
            return Ok(await mediator.SendAsync<SendInAppNotificationCommand, SendInAppNotificationResponse>(
                new SendInAppNotificationCommand(request), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>发送短信（开发环境 DryRun 模拟）</summary>
    /// <param name="request">发送请求</param>
    /// <param name="key">内部密钥（请求头 X-Internal-Key）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 发送结果；400 — 参数校验失败；401 — 内部密钥无效</returns>
    /// <response code="200">发送成功</response>
    /// <response code="400">参数校验失败</response>
    /// <response code="401">内部密钥无效</response>
    [HttpPost("sms")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SendSmsResponse>> SendSms(
        [FromBody] SendSmsRequest request,
        [FromHeader(Name = "X-Internal-Key")] string key,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_internalKey) || key != _internalKey)
            return Unauthorized(new { error = "内部密钥无效" });

        try
        {
            return Ok(await mediator.SendAsync<SendSmsCommand, SendSmsResponse>(
                new SendSmsCommand(request), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>发送 App Push（开发环境 DryRun 模拟）</summary>
    /// <param name="request">发送请求</param>
    /// <param name="key">内部密钥（请求头 X-Internal-Key）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 发送结果；400 — 参数校验失败；401 — 内部密钥无效</returns>
    /// <response code="200">发送成功</response>
    /// <response code="400">参数校验失败</response>
    /// <response code="401">内部密钥无效</response>
    [HttpPost("push")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SendPushResponse>> SendPush(
        [FromBody] SendPushRequest request,
        [FromHeader(Name = "X-Internal-Key")] string key,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_internalKey) || key != _internalKey)
            return Unauthorized(new { error = "内部密钥无效" });

        try
        {
            return Ok(await mediator.SendAsync<SendPushCommand, SendPushResponse>(
                new SendPushCommand(request), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }
}

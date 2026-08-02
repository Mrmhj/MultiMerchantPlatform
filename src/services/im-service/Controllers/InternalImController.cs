using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using ImService.Application.Commands;
using ImService.Application.Hubs;
using ImService.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace ImService.Controllers;

/// <summary>
/// 内部接口（服务间调用）— order/logistics 等系统服务推送订单/物流状态通知到买家会话，X-Internal-Key 校验。
/// </summary>
[ApiController]
[Route("api/im")]
[Produces("application/json")]
public sealed class InternalImController(IMediator mediator, IConfiguration configuration, MessageDispatcher dispatcher)
    : ControllerBase
{
    private readonly string _internalKey = configuration["Internal:Key"] ?? string.Empty;

    /// <summary>内部推送系统通知（订单状态 / 物流状态 / 平台公告；定位会话 → 落库 → 实时推送）</summary>
    /// <param name="request">通知内容 + 接收用户 + 商户</param>
    /// <param name="key">内部密钥（请求头 X-Internal-Key）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 推送结果（含是否实时送达）；400 — 内容为空；401 — 内部密钥无效；404 — 指定会话不存在</returns>
    /// <response code="200">推送成功</response>
    /// <response code="400">参数错误</response>
    /// <response code="401">内部密钥无效</response>
    /// <response code="404">会话不存在</response>
    [HttpPost("internal/push")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PushNotificationResponse>> Push(
        [FromBody] PushNotificationRequest request,
        [FromHeader(Name = "X-Internal-Key")] string key,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_internalKey) || key != _internalKey)
            return Unauthorized(new { error = "内部密钥无效" });

        try
        {
            var result = await mediator.SendAsync<PushNotificationCommand, PushNotificationResponse>(
                new(request.ToUserId, request.MerchantId, request.Content, request.MessageType, request.SessionId), ct);

            // 实时推送（用户在线则立即送达；离线消息由上线补推兜底）
            var message = await mediator.QueryAsync<Application.Queries.MessageByIdQuery, MessageResponse>(
                new(result.MessageId), ct);
            await dispatcher.SendToUserAsync(request.ToUserId, message);

            return Ok(new PushNotificationResponse
            {
                SessionId = result.SessionId,
                MessageId = result.MessageId,
                Delivered = dispatcher.IsOnline(request.ToUserId),
            });
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "会话不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }
}

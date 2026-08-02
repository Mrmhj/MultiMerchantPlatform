using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Security;
using ImService.Application.Commands;
using ImService.Application.Hubs;
using ImService.Application.Queries;
using ImService.Domain.Enums;
using ImService.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImService.Controllers;

/// <summary>
/// C 端买家聊天接口（api/im）— 会话列表 / 创建私聊 / 历史消息 / 已读 / 发送消息，需登录。
/// 实时通道为 SignalR Hub（/hub/chat），本控制器提供 REST 兜底（历史查询、离线操作）。
/// </summary>
[ApiController]
[Authorize]
[Route("api/im")]
[Produces("application/json")]
public sealed class BuyerImController(
    IMediator mediator,
    ICurrentUser currentUser,
    MessageDispatcher dispatcher) : ControllerBase
{
    /// <summary>我的会话列表（最新在前，含未读数）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 会话列表</returns>
    /// <response code="200">会话列表</response>
    [HttpGet("sessions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SessionResponse>>> MySessions(CancellationToken ct)
    {
        return Ok(await mediator.QueryAsync<MySessionsQuery, List<SessionResponse>>(
            new MySessionsQuery(currentUser.UserId), ct));
    }

    /// <summary>获取或创建与商户客服的私聊会话（幂等：已有活跃会话直接返回）</summary>
    /// <param name="request">商户 ID + 对方用户 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 新建会话；200 — 已有会话（幂等）；400 — 不能与自己会话</returns>
    /// <response code="200">已有会话</response>
    /// <response code="201">新建会话</response>
    /// <response code="400">参数错误</response>
    [HttpPost("sessions/private")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SessionResponse>> GetOrCreatePrivate(
        [FromBody] CreatePrivateSessionRequest request, CancellationToken ct)
    {
        try
        {
            var session = await mediator.SendAsync<GetOrCreatePrivateSessionCommand, SessionResponse>(
                new(currentUser.UserId, currentUser.UserName, request.MerchantId, request.PeerUserId, null), ct);

            // 已存在 → 200；新建（创建时间距今 < 1 秒）→ 201
            var createdRecently = (DateTime.UtcNow - session.CreatedAt) < TimeSpan.FromSeconds(1);
            return createdRecently ? Created("", session) : Ok(session);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>会话历史消息（游标分页，最新在前）</summary>
    /// <param name="id">会话 ID</param>
    /// <param name="beforeId">游标：仅返回早于该消息的消息（可选）</param>
    /// <param name="limit">每页条数（默认 50，上限 200）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 消息页；404 — 会话不存在</returns>
    /// <response code="200">消息页</response>
    /// <response code="404">会话不存在</response>
    [HttpGet("sessions/{id:guid}/messages")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessagePageResponse>> Messages(
        Guid id, [FromQuery] Guid? beforeId, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        try
        {
            return Ok(await mediator.QueryAsync<SessionMessagesQuery, MessagePageResponse>(
                new(id, currentUser.UserId, beforeId, limit), ct));
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

    /// <summary>标记会话全部已读（并广播已读回执给会话成员）</summary>
    /// <param name="id">会话 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 已读回执；404 — 会话不存在</returns>
    /// <response code="200">已读回执</response>
    /// <response code="404">会话不存在</response>
    [HttpPost("sessions/{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReadReceiptResponse>> MarkRead(Guid id, CancellationToken ct)
    {
        try
        {
            var receipt = await mediator.SendAsync<MarkSessionReadCommand, ReadReceiptResponse>(
                new(id, currentUser.UserId), ct);
            await dispatcher.NotifyReadAsync(id, currentUser.UserId, receipt.MarkedCount);
            return Ok(receipt);
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

    /// <summary>发送消息（REST 兜底通道，等价于 Hub 的 SendMessage）</summary>
    /// <param name="id">会话 ID</param>
    /// <param name="request">内容 + 消息类型</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 落库后的消息；400 — 内容为空/会话关闭/非成员；404 — 会话不存在</returns>
    /// <response code="200">发送成功</response>
    /// <response code="400">参数错误</response>
    /// <response code="404">会话不存在</response>
    [HttpPost("sessions/{id:guid}/send")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageResponse>> Send(
        Guid id, [FromBody] SendMessageRequest request, CancellationToken ct)
    {
        try
        {
            var message = await mediator.SendAsync<SendMessageCommand, MessageResponse>(
                new(id, currentUser.UserId, currentUser.UserName, ChatMemberRole.Buyer,
                    request.Content, request.MessageType), ct);
            await dispatcher.SendToSessionAsync(id, message);
            return Ok(message);
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

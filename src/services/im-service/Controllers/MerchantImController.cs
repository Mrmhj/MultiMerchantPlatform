using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.MultiTenant;
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
/// 商户端聊天接口（api/im/merchant）— 会话列表 / 群聊创建 / 历史消息 / 已读 / 回复，需登录 + X-Merchant-Id。
/// 多租户：请求头 X-Merchant-Id 定位商户，HasQueryFilter + Handler 显式过滤双重防护。
/// </summary>
[ApiController]
[Authorize]
[Route("api/im/merchant")]
[Produces("application/json")]
public sealed class MerchantImController(
    IMediator mediator,
    ICurrentUser currentUser,
    ITenantProvider tenantProvider,
    MessageDispatcher dispatcher) : ControllerBase
{
    /// <summary>本商户会话列表（最新在前，含未读数）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 会话列表；400 — 缺少 X-Merchant-Id</returns>
    /// <response code="200">会话列表</response>
    /// <response code="400">缺少商户头</response>
    [HttpGet("sessions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<SessionResponse>>> Sessions(CancellationToken ct)
    {
        try
        {
            var merchantId = tenantProvider.CurrentMerchantId
                ?? throw new DomainException("缺少 X-Merchant-Id 请求头", "MERCHANT_REQUIRED");
            return Ok(await mediator.QueryAsync<MerchantSessionsQuery, List<SessionResponse>>(
                new MerchantSessionsQuery(merchantId), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>创建客服群聊会话（发起人自动加入，成员去重）</summary>
    /// <param name="request">群名称 + 客服成员</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 新建群聊；400 — 参数错误</returns>
    /// <response code="201">创建成功</response>
    /// <response code="400">参数错误</response>
    [HttpPost("groups")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SessionResponse>> CreateGroup(
        [FromBody] CreateGroupSessionRequest request, CancellationToken ct)
    {
        try
        {
            var merchantId = tenantProvider.CurrentMerchantId
                ?? throw new DomainException("缺少 X-Merchant-Id 请求头", "MERCHANT_REQUIRED");
            var session = await mediator.SendAsync<CreateGroupSessionCommand, SessionResponse>(
                new(merchantId, request.Name, request.StaffUserIds, currentUser.UserId, currentUser.UserName), ct);
            return Created("", session);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>会话历史消息（游标分页，最新在前，校验商户归属）</summary>
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
            var merchantId = tenantProvider.CurrentMerchantId
                ?? throw new DomainException("缺少 X-Merchant-Id 请求头", "MERCHANT_REQUIRED");
            return Ok(await mediator.QueryAsync<SessionMessagesQuery, MessagePageResponse>(
                new(id, currentUser.UserId, beforeId, limit, merchantId), ct));
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
            var merchantId = tenantProvider.CurrentMerchantId
                ?? throw new DomainException("缺少 X-Merchant-Id 请求头", "MERCHANT_REQUIRED");
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

    /// <summary>回复消息（REST 兜底通道，等价于 Hub 的 SendMessage，角色为商户客服）</summary>
    /// <param name="id">会话 ID</param>
    /// <param name="request">内容 + 消息类型</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 落库后的消息；400 — 内容为空/会话关闭/非成员；404 — 会话不存在</returns>
    /// <response code="200">发送成功</response>
    /// <response code="400">参数错误</response>
    /// <response code="404">会话不存在</response>
    [HttpPost("sessions/{id:guid}/reply")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageResponse>> Reply(
        Guid id, [FromBody] SendMessageRequest request, CancellationToken ct)
    {
        try
        {
            var merchantId = tenantProvider.CurrentMerchantId
                ?? throw new DomainException("缺少 X-Merchant-Id 请求头", "MERCHANT_REQUIRED");
            var message = await mediator.SendAsync<SendMessageCommand, MessageResponse>(
                new(id, currentUser.UserId, currentUser.UserName, ChatMemberRole.MerchantStaff,
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

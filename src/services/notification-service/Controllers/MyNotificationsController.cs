using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using BuildingBlocks.Security;
using NotificationService.Application.Commands;
using NotificationService.Application.Hubs;
using NotificationService.Application.Queries;
using NotificationService.Domain.Enums;
using NotificationService.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace NotificationService.Controllers;

/// <summary>
/// 我的通知接口（用户端）— 通知收件箱：列表 / 未读数 / 已读 / 全部已读 / 删除，需登录。
/// 数据按 JWT 用户身份（sub）隔离，仅可操作自己的通知。
/// </summary>
[ApiController]
[Authorize]
[Route("api/notifications")]
[Produces("application/json")]
public sealed class MyNotificationsController(
    IMediator mediator,
    ICurrentUser currentUser,
    NotificationDispatcher dispatcher) : ControllerBase
{
    /// <summary>我的通知分页列表</summary>
    /// <param name="type">按业务类型过滤（可选）</param>
    /// <param name="isRead">按已读状态过滤（可选）</param>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 通知分页列表</returns>
    /// <response code="200">通知列表</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<NotificationResponse>>> List(
        [FromQuery] NotificationType? type,
        [FromQuery] bool? isRead,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await mediator.QueryAsync<MyNotificationsQuery, PagedResult<NotificationResponse>>(
            new MyNotificationsQuery(currentUser.UserId, type, isRead, page, pageSize), ct));

    /// <summary>未读通知数（客户端轮询/角标同步）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 未读数</returns>
    /// <response code="200">未读数</response>
    [HttpGet("unread-count")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountResponse>> UnreadCount(CancellationToken ct)
        => Ok(await mediator.QueryAsync<UnreadCountQuery, UnreadCountResponse>(
            new UnreadCountQuery(currentUser.UserId), ct));

    /// <summary>标记单条通知已读（实时推送未读数变化）</summary>
    /// <param name="id">通知 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 已读后的通知；404 — 通知不存在（或非本人）</returns>
    /// <response code="200">标记成功</response>
    /// <response code="404">通知不存在</response>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationResponse>> MarkRead(Guid id, CancellationToken ct)
    {
        try
        {
            var notification = await mediator.SendAsync<MarkNotificationReadCommand, NotificationResponse>(
                new MarkNotificationReadCommand(currentUser.UserId, id), ct);
            await dispatcher.NotifyUnreadAsync(currentUser.UserId, 0);
            return Ok(notification);
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "通知不存在" });
        }
    }

    /// <summary>全部标记已读</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 剩余未读数（0）</returns>
    /// <response code="200">全部已读</response>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountResponse>> MarkAllRead(CancellationToken ct)
    {
        var result = await mediator.SendAsync<MarkAllNotificationsReadCommand, UnreadCountResponse>(
            new MarkAllNotificationsReadCommand(currentUser.UserId), ct);
        await dispatcher.NotifyUnreadAsync(currentUser.UserId, result.UnreadCount);
        return Ok(result);
    }

    /// <summary>删除单条通知（软删除，移出收件箱）</summary>
    /// <param name="id">通知 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>204 — 删除成功；404 — 通知不存在（或非本人）</returns>
    /// <response code="204">删除成功</response>
    /// <response code="404">通知不存在</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await mediator.SendAsync<DeleteNotificationCommand, Unit>(
                new DeleteNotificationCommand(currentUser.UserId, id), ct);
            return NoContent();
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "通知不存在" });
        }
    }
}

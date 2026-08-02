using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using BuildingBlocks.Security;
using NotificationService.Application.Commands;
using NotificationService.Application.Queries;
using NotificationService.Domain.Enums;
using NotificationService.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NotificationService.Controllers;

/// <summary>
/// 公告接口 — 平台公告：发布/下架（admin）、列表/详情/已读/未读数（登录用户）。
/// 公告为广播模型（一对多），桌面端/商户端/管理端共用。
/// </summary>
[ApiController]
[Authorize]
[Route("api/notifications/announcements")]
[Produces("application/json")]
public sealed class AnnouncementsController(
    IMediator mediator,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>发布公告（平台 admin，创建即发布并广播给在线用户）</summary>
    /// <param name="request">公告内容</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 公告详情；400 — 参数校验失败；403 — 非 admin</returns>
    /// <response code="201">发布成功</response>
    /// <response code="400">参数校验失败</response>
    /// <response code="403">无权限</response>
    [HttpPost]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AnnouncementResponse>> Publish(
        [FromBody] PublishAnnouncementRequest request, CancellationToken ct)
    {
        try
        {
            var result = await mediator.SendAsync<PublishAnnouncementCommand, AnnouncementResponse>(
                new PublishAnnouncementCommand(request, currentUser.UserId, currentUser.UserName), ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>下线公告（平台 admin）</summary>
    /// <param name="id">公告 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 下线后公告；404 — 公告不存在；403 — 非 admin</returns>
    /// <response code="200">下线成功</response>
    /// <response code="403">无权限</response>
    /// <response code="404">公告不存在</response>
    [HttpPost("{id:guid}/offline")]
    [Authorize(Roles = "admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnouncementResponse>> Offline(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<OfflineAnnouncementCommand, AnnouncementResponse>(
                new OfflineAnnouncementCommand(id), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "公告不存在" });
        }
    }

    /// <summary>公告分页列表（已发布，含当前用户已读状态）</summary>
    /// <param name="category">按分类过滤（可选）</param>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 公告分页列表</returns>
    /// <response code="200">公告列表</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AnnouncementResponse>>> List(
        [FromQuery] AnnouncementCategory? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await mediator.QueryAsync<AnnouncementListQuery, PagedResult<AnnouncementResponse>>(
            new AnnouncementListQuery(currentUser.UserId, category, page, pageSize), ct));

    /// <summary>公告未读数（顶栏角标）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 未读数</returns>
    /// <response code="200">未读数</response>
    [HttpGet("unread-count")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountResponse>> UnreadCount(CancellationToken ct)
        => Ok(await mediator.QueryAsync<AnnouncementUnreadCountQuery, UnreadCountResponse>(
            new AnnouncementUnreadCountQuery(currentUser.UserId), ct));

    /// <summary>公告详情（含当前用户已读状态）</summary>
    /// <param name="id">公告 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 公告详情；404 — 公告不存在或未发布</returns>
    /// <response code="200">公告详情</response>
    /// <response code="404">公告不存在</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnouncementResponse>> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.QueryAsync<AnnouncementByIdQuery, AnnouncementResponse>(
                new AnnouncementByIdQuery(currentUser.UserId, id), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "公告不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>标记公告已读（幂等）</summary>
    /// <param name="id">公告 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 已读后公告；404 — 公告不存在或未发布</returns>
    /// <response code="200">标记成功</response>
    /// <response code="404">公告不存在</response>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnouncementResponse>> MarkRead(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<MarkAnnouncementReadCommand, AnnouncementResponse>(
                new MarkAnnouncementReadCommand(currentUser.UserId, id), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "公告不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }
}

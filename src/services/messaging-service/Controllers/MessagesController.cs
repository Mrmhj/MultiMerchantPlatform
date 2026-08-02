using BuildingBlocks.Core.Results;
using MessagingService.Application;
using MessagingService.Domain.Entities;
using MessagingService.Domain.Enums;
using MessagingService.DTOs;
using MessagingService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MessagingService.Controllers;

/// <summary>
/// 消息管理 API — 发布 / 查询 / 手动重试 / 死信管理。
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class MessagesController(
    MessagePublisher publisher,
    MessagingDbContext db) : ControllerBase
{
    /// <summary>发布一条消息（Outbox 落库，异步投递）</summary>
    /// <param name="request">发布消息请求（事件名 + JSON 负载）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 消息状态记录</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MessageResponse>> Publish([FromBody] PublishMessageRequest request, CancellationToken ct)
    {
        var messageId = await publisher.PublishRawAsync(
            request.EventName, request.Payload, request.RoutingKey,
            request.MaxRetryCount, request.MessageId, ct);

        var entity = await db.MessageOutboxes.SingleAsync(m => m.MessageId == messageId, ct);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToResponse(entity));
    }

    /// <summary>批量发布消息</summary>
    /// <param name="requests">发布消息请求列表（非空）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 消息状态记录数组</returns>
    [HttpPost("batch")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MessageResponse[]>> PublishBatch(
        [FromBody] IEnumerable<PublishMessageRequest> requests, CancellationToken ct)
    {
        var list = requests.ToList();
        if (list.Count == 0)
            return BadRequest("请求列表不能为空");

        foreach (var request in list)
        {
            await publisher.PublishRawAsync(
                request.EventName, request.Payload, request.RoutingKey,
                request.MaxRetryCount, request.MessageId, ct);
        }

        var ids = list.Select(r => r.MessageId ?? Guid.Empty).ToList();
        var entities = await db.MessageOutboxes
            .Where(m => ids.Contains(m.MessageId))
            .ToListAsync(ct);

        return Created("", entities.Select(ToResponse).ToArray());
    }

    /// <summary>按 Id 查询消息状态</summary>
    /// <param name="id">消息记录 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 消息状态；404 — 消息不存在</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageResponse>> GetById(Guid id, CancellationToken ct)
    {
        var entity = await db.MessageOutboxes.FindAsync([id], ct);
        return entity is null ? NotFound($"消息 {id} 不存在") : Ok(ToResponse(entity));
    }

    /// <summary>分页查询消息（支持状态 / 事件名过滤）</summary>
    /// <param name="status">按状态过滤（可选）</param>
    /// <param name="eventName">按事件名过滤（可选）</param>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 分页消息列表</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MessageResponse>>> Query(
        [FromQuery] MessageStatus? status,
        [FromQuery] string? eventName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.MessageOutboxes.AsNoTracking().AsQueryable();
        if (status.HasValue)
            query = query.Where(m => m.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(eventName))
            query = query.Where(m => m.EventName == eventName);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new PagedResult<MessageResponse>(
            items.Select(ToResponse).ToList(), total, page, pageSize));
    }

    /// <summary>手动重试（将死信 / 失败消息重置为待发送）</summary>
    /// <param name="id">消息记录 ID</param>
    /// <param name="timeProvider">时间提供器（注入，可测试）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 重置后的消息状态；404 — 消息不存在；400 — 已发布无需重试</returns>
    [HttpPost("{id:guid}/retry")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageResponse>> Retry(Guid id, TimeProvider timeProvider, CancellationToken ct)
    {
        var entity = await db.MessageOutboxes.FindAsync([id], ct);
        if (entity is null)
            return NotFound($"消息 {id} 不存在");

        if (entity.Status == MessageStatus.Published)
            return BadRequest($"消息 {id} 已发布，无需重试");

        entity.ResetForRetry(timeProvider);
        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(entity));
    }

    /// <summary>手动转死信</summary>
    /// <param name="id">消息记录 ID</param>
    /// <param name="reason">转死信原因（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 转死信后的消息状态；404 — 消息不存在</returns>
    [HttpPost("{id:guid}/deadletter")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageResponse>> MoveToDeadLetter(Guid id, [FromQuery] string? reason, CancellationToken ct)
    {
        var entity = await db.MessageOutboxes.FindAsync([id], ct);
        if (entity is null)
            return NotFound($"消息 {id} 不存在");

        entity.MoveToDeadLetter(reason);
        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(entity));
    }

    private static MessageResponse ToResponse(MessageOutbox m) => new()
    {
        Id = m.Id,
        MessageId = m.MessageId,
        EventName = m.EventName,
        Payload = m.Payload,
        RoutingKey = m.RoutingKey,
        Status = m.Status,
        RetryCount = m.RetryCount,
        MaxRetryCount = m.MaxRetryCount,
        NextRetryTime = m.NextRetryTime,
        PublishedAt = m.PublishedAt,
        LastError = m.LastError,
        CreatedAt = m.CreatedAt,
    };
}

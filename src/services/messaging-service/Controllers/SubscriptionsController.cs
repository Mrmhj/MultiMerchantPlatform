using MessagingService.Application;
using MessagingService.Domain.Entities;
using MessagingService.DTOs;
using MessagingService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MessagingService.Controllers;

/// <summary>
/// 订阅管理 API — 注册 / 查询 / 取消订阅。
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class SubscriptionsController(SubscriptionManager manager, MessagingDbContext db) : ControllerBase
{
    /// <summary>注册订阅（已存在则重新激活，幂等）</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SubscriptionResponse>> Register(
        [FromBody] RegisterSubscriptionRequest request, CancellationToken ct)
    {
        var subscription = await manager.RegisterAsync(
            request.EventName, request.CallbackUrl, request.ServiceName, request.MaxRetryCount, ct);
        return CreatedAtAction(nameof(List), new { }, ToResponse(subscription));
    }

    /// <summary>查询订阅列表（支持事件名过滤）</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubscriptionResponse>>> List(
        [FromQuery] string? eventName,
        [FromQuery] bool? active,
        CancellationToken ct = default)
    {
        var query = db.Subscriptions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(eventName))
            query = query.Where(s => s.EventName == eventName);
        if (active.HasValue)
            query = query.Where(s => s.IsActive == active.Value);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        return Ok(items.Select(ToResponse).ToList());
    }

    /// <summary>取消订阅（软停用）</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unregister(Guid id, CancellationToken ct)
    {
        var removed = await manager.UnregisterAsync(id, ct);
        return removed ? NoContent() : NotFound($"订阅 {id} 不存在");
    }

    /// <summary>启用订阅</summary>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionResponse>> Activate(Guid id, CancellationToken ct)
    {
        var activated = await manager.ActivateAsync(id, ct);
        if (!activated)
            return NotFound($"订阅 {id} 不存在");

        var subscription = await db.Subscriptions.FindAsync([id], ct);
        return Ok(ToResponse(subscription!));
    }

    private static SubscriptionResponse ToResponse(MessageSubscription s) => new()
    {
        Id = s.Id,
        EventName = s.EventName,
        CallbackUrl = s.CallbackUrl,
        ServiceName = s.ServiceName,
        MaxRetryCount = s.MaxRetryCount,
        IsActive = s.IsActive,
        CreatedAt = s.CreatedAt,
    };
}

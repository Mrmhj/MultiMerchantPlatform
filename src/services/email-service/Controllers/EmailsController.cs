using BuildingBlocks.Core.Results;
using EmailService.Application;
using EmailService.Domain.Entities;
using EmailService.Domain.Enums;
using EmailService.DTOs;
using EmailService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmailService.Controllers;

/// <summary>
/// 邮件 API — 发送 / 批量 / 状态查询 / 手动重试 / 死信管理。
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class EmailsController(EmailSender sender, EmailDbContext db) : ControllerBase
{
    /// <summary>发送一封邮件（支持模板渲染，失败自动进入重试队列）</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmailResponse>> Send([FromBody] SendEmailRequest request, CancellationToken ct)
    {
        var email = await sender.SendAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = email.Id }, ToResponse(email));
    }

    /// <summary>批量发送邮件</summary>
    [HttpPost("batch")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<EmailResponse[]>> SendBatch(
        [FromBody] IEnumerable<SendEmailRequest> requests, CancellationToken ct)
    {
        var list = requests.ToList();
        if (list.Count == 0)
            return BadRequest("请求列表不能为空");

        var emails = await sender.SendBatchAsync(list, ct);
        return Created("", emails.Select(ToResponse).ToArray());
    }

    /// <summary>分页查询邮件（状态/收件人过滤）</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EmailResponse>>> Query(
        [FromQuery] EmailStatus? status,
        [FromQuery] string? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Emails.AsNoTracking().AsQueryable();
        if (status.HasValue)
            query = query.Where(e => e.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(to))
            query = query.Where(e => e.To.Contains(to.Trim()));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new PagedResult<EmailResponse>(items.Select(ToResponse).ToList(), total, page, pageSize));
    }

    /// <summary>按 Id 查询邮件</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmailResponse>> GetById(Guid id, CancellationToken ct)
    {
        var email = await db.Emails.FindAsync([id], ct);
        return email is null ? NotFound($"邮件 {id} 不存在") : Ok(ToResponse(email));
    }

    /// <summary>手动重试（重置失败/死信邮件）</summary>
    [HttpPost("{id:guid}/retry")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmailResponse>> Retry(Guid id, CancellationToken ct)
    {
        var email = await sender.RetryAsync(id, ct);
        return email is null ? NotFound($"邮件 {id} 不存在") : Ok(ToResponse(email));
    }

    /// <summary>手动转死信</summary>
    [HttpPost("{id:guid}/deadletter")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmailResponse>> MoveToDeadLetter(Guid id, [FromQuery] string? reason, CancellationToken ct)
    {
        var email = await db.Emails.FindAsync([id], ct);
        if (email is null)
            return NotFound($"邮件 {id} 不存在");

        email.MoveToDeadLetter(reason);
        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(email));
    }

    private static EmailResponse ToResponse(EmailMessage e) => new()
    {
        Id = e.Id,
        From = e.From,
        To = e.To,
        Subject = e.Subject,
        IsHtml = e.IsHtml,
        TemplateName = e.TemplateName,
        Status = e.Status,
        RetryCount = e.RetryCount,
        MaxRetryCount = e.MaxRetryCount,
        SentAt = e.SentAt,
        LastError = e.LastError,
        CreatedAt = e.CreatedAt,
    };
}

using EmailService.Domain.Entities;
using EmailService.DTOs;
using EmailService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EmailService.Controllers;

/// <summary>
/// 邮件模板 API — 模板 CRUD（Razor 模板，供发送时渲染）。
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class TemplatesController(EmailDbContext db) : ControllerBase
{
    /// <summary>创建模板</summary>
    /// <param name="request">模板请求（名称唯一 + Razor 主题/正文模板）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 模板记录；409 — 模板名已存在</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<TemplateResponse>> Create([FromBody] TemplateRequest request, CancellationToken ct)
    {
        var exists = await db.Templates.AnyAsync(t => t.Name == request.Name, ct);
        if (exists)
            return Conflict($"模板 {request.Name} 已存在");

        var template = new EmailTemplate(request.Name, request.SubjectTemplate, request.BodyTemplate, request.Description);
        db.Templates.Add(template);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetByName), new { name = template.Name }, ToResponse(template));
    }

    /// <summary>按名称查询模板</summary>
    /// <param name="name">模板名</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 模板记录；404 — 模板不存在</returns>
    [HttpGet("{name}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TemplateResponse>> GetByName(string name, CancellationToken ct)
    {
        var template = await db.Templates.FirstOrDefaultAsync(t => t.Name == name, ct);
        return template is null ? NotFound($"模板 {name} 不存在") : Ok(ToResponse(template));
    }

    /// <summary>模板列表</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 模板列表（按名称排序）</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TemplateResponse>>> List(CancellationToken ct)
    {
        var templates = await db.Templates.AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
        return Ok(templates.Select(ToResponse).ToList());
    }

    /// <summary>更新模板</summary>
    /// <param name="name">模板名</param>
    /// <param name="request">新的模板内容</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的模板记录；404 — 模板不存在</returns>
    [HttpPut("{name}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TemplateResponse>> Update(string name, [FromBody] TemplateRequest request, CancellationToken ct)
    {
        var template = await db.Templates.FirstOrDefaultAsync(t => t.Name == name, ct);
        if (template is null)
            return NotFound($"模板 {name} 不存在");

        template.Update(request.SubjectTemplate, request.BodyTemplate, request.Description);
        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(template));
    }

    /// <summary>启用模板</summary>
    /// <param name="name">模板名</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 启用后的模板记录；404 — 模板不存在</returns>
    [HttpPost("{name}/activate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<TemplateResponse>> Activate(string name, CancellationToken ct)
    {
        var template = await db.Templates.FirstOrDefaultAsync(t => t.Name == name, ct);
        if (template is null)
            return NotFound($"模板 {name} 不存在");

        template.Activate();
        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(template));
    }

    /// <summary>停用模板</summary>
    /// <param name="name">模板名</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 停用后的模板记录；404 — 模板不存在</returns>
    [HttpPost("{name}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<TemplateResponse>> Deactivate(string name, CancellationToken ct)
    {
        var template = await db.Templates.FirstOrDefaultAsync(t => t.Name == name, ct);
        if (template is null)
            return NotFound($"模板 {name} 不存在");

        template.Deactivate();
        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(template));
    }

    /// <summary>删除模板</summary>
    /// <param name="name">模板名</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>204 — 已删除；404 — 模板不存在</returns>
    [HttpDelete("{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string name, CancellationToken ct)
    {
        var template = await db.Templates.FirstOrDefaultAsync(t => t.Name == name, ct);
        if (template is null)
            return NotFound($"模板 {name} 不存在");

        db.Templates.Remove(template);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static TemplateResponse ToResponse(EmailTemplate t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        SubjectTemplate = t.SubjectTemplate,
        BodyTemplate = t.BodyTemplate,
        Description = t.Description,
        IsActive = t.IsActive,
        CreatedAt = t.CreatedAt,
    };
}

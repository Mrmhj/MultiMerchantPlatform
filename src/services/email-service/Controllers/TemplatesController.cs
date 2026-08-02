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
    [HttpGet("{name}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TemplateResponse>> GetByName(string name, CancellationToken ct)
    {
        var template = await db.Templates.FirstOrDefaultAsync(t => t.Name == name, ct);
        return template is null ? NotFound($"模板 {name} 不存在") : Ok(ToResponse(template));
    }

    /// <summary>模板列表</summary>
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

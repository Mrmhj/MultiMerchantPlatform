using BuildingBlocks.Core.Entities;

namespace EmailService.Domain.Entities;

/// <summary>
/// 邮件模板 — Razor 模板（RazorLight 渲染）。
/// </summary>
public sealed class EmailTemplate : Entity
{
    private EmailTemplate() { } // EF Core

    public EmailTemplate(string name, string subjectTemplate, string bodyTemplate, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectTemplate);
        ArgumentException.ThrowIfNullOrWhiteSpace(bodyTemplate);

        Name = name;
        SubjectTemplate = subjectTemplate;
        BodyTemplate = bodyTemplate;
        Description = description;
        IsActive = true;
    }

    /// <summary>模板名（唯一，如 Welcome / OrderConfirmed）</summary>
    public string Name { get; private set; } = null!;

    /// <summary>主题模板（Razor 语法，支持变量）</summary>
    public string SubjectTemplate { get; private set; } = null!;

    /// <summary>正文模板（Razor HTML）</summary>
    public string BodyTemplate { get; private set; } = null!;

    /// <summary>模板说明</summary>
    public string? Description { get; private set; }

    /// <summary>是否启用</summary>
    public bool IsActive { get; private set; }

    public void Update(string subjectTemplate, string bodyTemplate, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectTemplate);
        ArgumentException.ThrowIfNullOrWhiteSpace(bodyTemplate);

        SubjectTemplate = subjectTemplate;
        BodyTemplate = bodyTemplate;
        Description = description;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}

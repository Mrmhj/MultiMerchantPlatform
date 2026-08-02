using BuildingBlocks.Core.Entities;

namespace EmailService.Domain.Entities;

/// <summary>
/// 邮件模板 — Razor 模板（RazorLight 渲染）。
/// </summary>
public sealed class EmailTemplate : Entity
{
    private EmailTemplate() { } // EF Core

    /// <summary>创建邮件模板（默认启用）</summary>
    /// <param name="name">模板名（唯一）</param>
    /// <param name="subjectTemplate">主题模板（Razor 语法）</param>
    /// <param name="bodyTemplate">正文模板（Razor HTML）</param>
    /// <param name="description">模板说明（可选）</param>
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

    /// <summary>更新模板内容</summary>
    /// <param name="subjectTemplate">新主题模板</param>
    /// <param name="bodyTemplate">新正文模板</param>
    /// <param name="description">新说明</param>
    public void Update(string subjectTemplate, string bodyTemplate, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectTemplate);
        ArgumentException.ThrowIfNullOrWhiteSpace(bodyTemplate);

        SubjectTemplate = subjectTemplate;
        BodyTemplate = bodyTemplate;
        Description = description;
    }

    /// <summary>启用模板</summary>
    public void Activate() => IsActive = true;

    /// <summary>停用模板（暂停使用，不删除）</summary>
    public void Deactivate() => IsActive = false;
}

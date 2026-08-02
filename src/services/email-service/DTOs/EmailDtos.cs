using System.ComponentModel.DataAnnotations;
using EmailService.Domain.Enums;

namespace EmailService.DTOs;

/// <summary>发送邮件请求</summary>
public sealed record SendEmailRequest
{
    /// <summary>收件人（多个用 ; 分隔）</summary>
    [Required, StringLength(1000, MinimumLength = 3)]
    public required string To { get; init; }

    /// <summary>主题（指定模板时可不传，由模板渲染）</summary>
    [StringLength(500)]
    public string? Subject { get; init; }

    /// <summary>正文（指定模板时可不传，由模板渲染）</summary>
    public string? Body { get; init; }

    /// <summary>模板名（可选，如 Welcome / OrderConfirmed）</summary>
    [StringLength(100)]
    public string? TemplateName { get; init; }

    /// <summary>模板渲染数据（字典键与模板 @Model.xxx 对应）</summary>
    public Dictionary<string, object?>? TemplateData { get; init; }

    /// <summary>是否 HTML 正文（默认 true）</summary>
    public bool? IsHtml { get; init; }

    /// <summary>抄送（; 分隔）</summary>
    [StringLength(1000)]
    public string? Cc { get; init; }

    /// <summary>密送（; 分隔）</summary>
    [StringLength(1000)]
    public string? Bcc { get; init; }

    /// <summary>覆盖默认最大重试次数（可选）</summary>
    [Range(1, 10)]
    public int? MaxRetryCount { get; init; }
}

/// <summary>邮件响应</summary>
public sealed record EmailResponse
{
    public Guid Id { get; init; }
    public required string From { get; init; }
    public required string To { get; init; }
    public required string Subject { get; init; }
    public bool IsHtml { get; init; }
    public string? TemplateName { get; init; }
    public EmailStatus Status { get; init; }
    public int RetryCount { get; init; }
    public int MaxRetryCount { get; init; }
    public DateTime? SentAt { get; init; }
    public string? LastError { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>模板创建/更新请求</summary>
public sealed record TemplateRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public required string Name { get; init; }

    /// <summary>主题模板（Razor，如 欢迎您，@Model.UserName！）</summary>
    [Required, StringLength(500, MinimumLength = 1)]
    public required string SubjectTemplate { get; init; }

    /// <summary>正文模板（Razor HTML）</summary>
    [Required]
    public required string BodyTemplate { get; init; }

    [StringLength(500)]
    public string? Description { get; init; }
}

/// <summary>模板响应</summary>
public sealed record TemplateResponse
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string SubjectTemplate { get; init; }
    public required string BodyTemplate { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}

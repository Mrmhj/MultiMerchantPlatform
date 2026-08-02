using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;
using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

/// <summary>
/// 通知模板 — 预定义标题/内容模板，支持 {占位符} 变量渲染，供各服务按业务场景一键发送。
/// 平台级配置，不按商户隔离。
/// </summary>
public sealed class NotificationTemplate : Entity, IAggregateRoot
{
    private NotificationTemplate() { } // EF Core

    /// <summary>创建通知模板</summary>
    /// <param name="code">模板编码（唯一，如 ORDER_PAID）</param>
    /// <param name="titleTemplate">标题模板（可含 {变量}）</param>
    /// <param name="bodyTemplate">内容模板（可含 {变量}）</param>
    /// <param name="channels">适用渠道（位标志）</param>
    /// <param name="description">模板说明（可选）</param>
    public NotificationTemplate(
        string code, string titleTemplate, string bodyTemplate,
        NotificationChannel channels, string? description = null)
    {
        Code = ValidateCode(code);
        TitleTemplate = ValidateTemplate(titleTemplate, nameof(TitleTemplate));
        BodyTemplate = ValidateTemplate(bodyTemplate, nameof(BodyTemplate));
        Channels = channels;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>模板编码（唯一）</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>标题模板（可含 {变量}）</summary>
    public string TitleTemplate { get; private set; } = string.Empty;

    /// <summary>内容模板（可含 {变量}）</summary>
    public string BodyTemplate { get; private set; } = string.Empty;

    /// <summary>适用渠道（位标志）</summary>
    public NotificationChannel Channels { get; private set; }

    /// <summary>模板说明</summary>
    public string? Description { get; private set; }

    /// <summary>是否启用（停用后发送端不可用）</summary>
    public bool IsActive { get; private set; }

    /// <summary>更新模板</summary>
    /// <param name="code">模板编码</param>
    /// <param name="titleTemplate">标题模板</param>
    /// <param name="bodyTemplate">内容模板</param>
    /// <param name="channels">适用渠道</param>
    /// <param name="description">模板说明</param>
    public void Update(string code, string titleTemplate, string bodyTemplate,
        NotificationChannel channels, string? description)
    {
        Code = ValidateCode(code);
        TitleTemplate = ValidateTemplate(titleTemplate, nameof(TitleTemplate));
        BodyTemplate = ValidateTemplate(bodyTemplate, nameof(BodyTemplate));
        Channels = channels;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>启用模板</summary>
    public void Enable() => IsActive = true;

    /// <summary>停用模板（发送端拒绝使用）</summary>
    public void Disable() => IsActive = false;

    /// <summary>渲染标题（{变量} 占位符替换，未知变量替换为空串）</summary>
    /// <param name="data">变量字典</param>
    /// <returns>渲染后的标题</returns>
    public string RenderTitle(IReadOnlyDictionary<string, object?> data) => Render(TitleTemplate, data);

    /// <summary>渲染内容（{变量} 占位符替换，未知变量替换为空串）</summary>
    /// <param name="data">变量字典</param>
    /// <returns>渲染后的内容</returns>
    public string RenderBody(IReadOnlyDictionary<string, object?> data) => Render(BodyTemplate, data);

    private static string Render(string template, IReadOnlyDictionary<string, object?> data)
    {
        var result = template;
        foreach (var (key, value) in data)
        {
            result = result.Replace("{" + key + "}", value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        return result.Trim();
    }

    private static string ValidateCode(string code)
    {
        var trimmed = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (trimmed.Length is < 2 or > 50)
            throw new DomainException("模板编码长度需在 2-50 字符之间", "INVALID_TEMPLATE_CODE");
        if (!trimmed.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            throw new DomainException("模板编码仅允许字母、数字、下划线", "INVALID_TEMPLATE_CODE");
        return trimmed;
    }

    private static string ValidateTemplate(string template, string field)
    {
        var trimmed = (template ?? string.Empty).Trim();
        if (trimmed.Length is < 1 or > 2000)
            throw new DomainException($"{field} 长度需在 1-2000 字符之间", "INVALID_TEMPLATE_BODY");
        return trimmed;
    }
}

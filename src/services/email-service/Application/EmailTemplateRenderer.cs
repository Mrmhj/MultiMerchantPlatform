using System.Dynamic;
using EmailService.Domain.Entities;
using RazorLight;

namespace EmailService.Application;

/// <summary>
/// Razor 模板渲染器（RazorLight）。
/// 模板数据以字典传入，渲染时转为 ExpandoObject 供 @Model.xxx 访问。
/// </summary>
public sealed class EmailTemplateRenderer
{
    private readonly RazorLightEngine _engine;

    public EmailTemplateRenderer()
    {
        _engine = new RazorLightEngineBuilder()
            .UseMemoryCachingProvider()
            .Build();
    }

    /// <summary>渲染模板的主题与正文</summary>
    public async Task<(string Subject, string Body)> RenderAsync(
        EmailTemplate template, IDictionary<string, object?> data)
    {
        var model = ToExpando(data);
        var subject = await _engine.CompileRenderStringAsync(
            $"subject_{template.Id}", template.SubjectTemplate, model);
        var body = await _engine.CompileRenderStringAsync(
            $"body_{template.Id}", template.BodyTemplate, model);
        return (subject.Trim(), body.Trim());
    }

    private static ExpandoObject ToExpando(IDictionary<string, object?> data)
    {
        var expando = new ExpandoObject();
        var dict = (IDictionary<string, object?>)expando;
        foreach (var (key, value) in data)
            dict[key] = value;
        return expando;
    }
}

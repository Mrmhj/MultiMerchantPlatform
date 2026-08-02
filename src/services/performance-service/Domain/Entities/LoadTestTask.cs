using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;

namespace PerformanceService.Domain.Entities;

/// <summary>
/// 压测任务定义 — 描述一次压测的配置（目标 URL / 方法 / 并发 / 时长 / 请求体）。
/// 平台端（admin）创建，执行时由 <see cref="LoadTestRun"/> 记录每次运行批次。
/// </summary>
public sealed class LoadTestTask : Entity, IAggregateRoot
{
    private LoadTestTask() { } // EF Core

    /// <summary>创建压测任务</summary>
    /// <param name="name">任务名称</param>
    /// <param name="targetUrl">压测目标 URL（http/https）</param>
    /// <param name="httpMethod">HTTP 方法（GET/POST/PUT/DELETE）</param>
    /// <param name="concurrency">并发数（1-500）</param>
    /// <param name="durationSeconds">持续时间（秒，1-3600）</param>
    /// <param name="bodyJson">请求体 JSON（可选）</param>
    /// <param name="headersJson">请求头 JSON（可选，格式 {"name":"value"}）</param>
    [SetsRequiredMembers]
    public LoadTestTask(string name, string targetUrl, string httpMethod, int concurrency, int durationSeconds, string? bodyJson = null, string? headersJson = null)
    {
        Name = ValidateName(name);
        TargetUrl = ValidateUrl(targetUrl);
        HttpMethod = ValidateMethod(httpMethod);
        Concurrency = ValidateConcurrency(concurrency);
        DurationSeconds = ValidateDuration(durationSeconds);
        BodyJson = bodyJson;
        HeadersJson = headersJson;
        Enabled = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = null;
    }

    /// <summary>任务名称</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>压测目标 URL</summary>
    public string TargetUrl { get; private set; } = string.Empty;

    /// <summary>HTTP 方法（GET/POST/PUT/DELETE）</summary>
    public string HttpMethod { get; private set; } = "GET";

    /// <summary>并发数</summary>
    public int Concurrency { get; private set; }

    /// <summary>持续时间（秒）</summary>
    public int DurationSeconds { get; private set; }

    /// <summary>请求体 JSON（可选）</summary>
    public string? BodyJson { get; private set; }

    /// <summary>请求头 JSON（可选）</summary>
    public string? HeadersJson { get; private set; }

    /// <summary>是否启用（停用的任务不允许启动压测）</summary>
    public bool Enabled { get; private set; }

    /// <summary>更新任务配置</summary>
    /// <param name="name">任务名称</param>
    /// <param name="targetUrl">目标 URL</param>
    /// <param name="httpMethod">HTTP 方法</param>
    /// <param name="concurrency">并发数</param>
    /// <param name="durationSeconds">持续时间</param>
    /// <param name="bodyJson">请求体（可选）</param>
    /// <param name="headersJson">请求头（可选）</param>
    public void Update(string name, string targetUrl, string httpMethod, int concurrency, int durationSeconds, string? bodyJson, string? headersJson)
    {
        Name = ValidateName(name);
        TargetUrl = ValidateUrl(targetUrl);
        HttpMethod = ValidateMethod(httpMethod);
        Concurrency = ValidateConcurrency(concurrency);
        DurationSeconds = ValidateDuration(durationSeconds);
        BodyJson = bodyJson;
        HeadersJson = headersJson;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>启用任务</summary>
    public void Enable() => Enabled = true;

    /// <summary>停用任务（禁用后不可启动压测）</summary>
    public void Disable() => Enabled = false;

    private static string ValidateName(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length is < 2 or > 100)
            throw new DomainException("任务名称长度需在 2-100 字符之间", "INVALID_TASK_NAME");
        return trimmed;
    }

    private static string ValidateUrl(string url)
    {
        var trimmed = (url ?? string.Empty).Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new DomainException("目标 URL 必须是合法的 http/https 地址", "INVALID_TARGET_URL");
        return trimmed;
    }

    private static string ValidateMethod(string method)
    {
        var upper = (method ?? string.Empty).Trim().ToUpperInvariant();
        if (upper is not ("GET" or "POST" or "PUT" or "DELETE"))
            throw new DomainException("HTTP 方法仅支持 GET/POST/PUT/DELETE", "INVALID_HTTP_METHOD");
        return upper;
    }

    private static int ValidateConcurrency(int concurrency)
    {
        if (concurrency is < 1 or > 500)
            throw new DomainException("并发数需在 1-500 之间", "INVALID_CONCURRENCY");
        return concurrency;
    }

    private static int ValidateDuration(int durationSeconds)
    {
        if (durationSeconds is < 1 or > 3600)
            throw new DomainException("持续时间需在 1-3600 秒之间", "INVALID_DURATION");
        return durationSeconds;
    }
}

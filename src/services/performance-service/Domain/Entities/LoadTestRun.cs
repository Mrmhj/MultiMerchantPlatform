using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;
using PerformanceService.Domain.Enums;

namespace PerformanceService.Domain.Entities;

/// <summary>
/// 压测运行批次 — 记录一次压测执行的实时统计与最终结果。
/// 状态机：Queued → Running → Completed / Failed / Cancelled。
/// </summary>
public sealed class LoadTestRun : Entity, IAggregateRoot
{
    private LoadTestRun() { } // EF Core

    /// <summary>创建运行批次（初始 Queued）</summary>
    /// <param name="taskId">所属压测任务 ID</param>
    /// <param name="taskName">任务名称（快照）</param>
    /// <param name="targetUrl">目标 URL（快照）</param>
    /// <param name="httpMethod">HTTP 方法（快照）</param>
    /// <param name="concurrency">并发数（快照）</param>
    /// <param name="durationSeconds">持续时间（快照）</param>
    /// <param name="bodyJson">请求体 JSON（快照，可选）</param>
    /// <param name="headersJson">请求头 JSON（快照，可选）</param>
    /// <param name="now">创建时间（UTC）</param>
    [SetsRequiredMembers]
    public LoadTestRun(Guid taskId, string taskName, string targetUrl, string httpMethod, int concurrency, int durationSeconds, string? bodyJson, string? headersJson, DateTime now)
    {
        TaskId = taskId;
        TaskName = (taskName ?? string.Empty).Trim();
        TargetUrl = targetUrl;
        HttpMethod = (httpMethod ?? "GET").Trim().ToUpperInvariant();
        Concurrency = concurrency;
        DurationSeconds = durationSeconds;
        BodyJson = bodyJson;
        HeadersJson = headersJson;
        Status = LoadTestStatus.Queued;
        CreatedAt = now;
    }

    /// <summary>所属压测任务 ID</summary>
    public Guid TaskId { get; private set; }

    /// <summary>任务名称（快照）</summary>
    public string TaskName { get; private set; } = string.Empty;

    /// <summary>目标 URL（快照）</summary>
    public string TargetUrl { get; private set; } = string.Empty;

    /// <summary>HTTP 方法（快照）</summary>
    public string HttpMethod { get; private set; } = "GET";

    /// <summary>请求体 JSON（快照）</summary>
    public string? BodyJson { get; private set; }

    /// <summary>请求头 JSON（快照）</summary>
    public string? HeadersJson { get; private set; }

    /// <summary>并发数（快照）</summary>
    public int Concurrency { get; private set; }

    /// <summary>持续时间（秒，快照）</summary>
    public int DurationSeconds { get; private set; }

    /// <summary>状态</summary>
    public LoadTestStatus Status { get; private set; }

    /// <summary>开始时间（UTC）</summary>
    public DateTime? StartedAt { get; private set; }

    /// <summary>结束时间（UTC）</summary>
    public DateTime? FinishedAt { get; private set; }

    /// <summary>总请求数</summary>
    public long TotalRequests { get; private set; }

    /// <summary>成功请求数（2xx）</summary>
    public long SuccessCount { get; private set; }

    /// <summary>失败请求数（非 2xx / 网络异常 / 超时）</summary>
    public long FailCount { get; private set; }

    /// <summary>每秒请求数（QPS）</summary>
    public double Qps { get; private set; }

    /// <summary>平均延迟（毫秒）</summary>
    public double AvgLatencyMs { get; private set; }

    /// <summary>P50 延迟（毫秒）</summary>
    public double P50Ms { get; private set; }

    /// <summary>P95 延迟（毫秒）</summary>
    public double P95Ms { get; private set; }

    /// <summary>P99 延迟（毫秒）</summary>
    public double P99Ms { get; private set; }

    /// <summary>最大延迟（毫秒）</summary>
    public double MaxLatencyMs { get; private set; }

    /// <summary>错误率（百分比，0-100）</summary>
    public double ErrorRatePercent { get; private set; }

    /// <summary>HTML 报告相对路径（完成后生成）</summary>
    public string? ReportPath { get; private set; }

    /// <summary>失败原因（Failed 时填写）</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>标记开始执行（Queued → Running）</summary>
    /// <param name="now">开始时间（UTC）</param>
    public void MarkRunning(DateTime now)
    {
        if (Status != LoadTestStatus.Queued)
            throw new DomainException($"当前状态不允许启动压测（{Status}）", "LOADTEST_STATE_INVALID");

        Status = LoadTestStatus.Running;
        StartedAt = now;
    }

    /// <summary>写入运行统计并标记完成（Running → Completed）</summary>
    /// <param name="stats">压测统计结果</param>
    /// <param name="now">完成时间（UTC）</param>
    /// <param name="reportPath">HTML 报告相对路径（可选）</param>
    public void Complete(LoadTestStatistics stats, DateTime now, string? reportPath = null)
    {
        if (Status != LoadTestStatus.Running)
            throw new DomainException($"当前状态不允许写入结果（{Status}）", "LOADTEST_STATE_INVALID");

        TotalRequests = stats.TotalRequests;
        SuccessCount = stats.SuccessCount;
        FailCount = stats.FailCount;
        Qps = stats.Qps;
        AvgLatencyMs = stats.AvgLatencyMs;
        P50Ms = stats.P50Ms;
        P95Ms = stats.P95Ms;
        P99Ms = stats.P99Ms;
        MaxLatencyMs = stats.MaxLatencyMs;
        ErrorRatePercent = stats.ErrorRatePercent;
        ReportPath = reportPath;
        Status = LoadTestStatus.Completed;
        FinishedAt = now;
    }

    /// <summary>标记失败（Running → Failed）</summary>
    /// <param name="reason">失败原因</param>
    /// <param name="now">失败时间（UTC）</param>
    public void Fail(string reason, DateTime now)
    {
        if (Status is LoadTestStatus.Completed or LoadTestStatus.Failed)
            throw new DomainException($"当前状态不允许标记失败（{Status}）", "LOADTEST_STATE_INVALID");

        Status = LoadTestStatus.Failed;
        ErrorMessage = reason;
        FinishedAt = now;
    }

    /// <summary>标记取消（Running → Cancelled）</summary>
    /// <param name="now">取消时间（UTC）</param>
    public void Cancel(DateTime now)
    {
        if (Status is LoadTestStatus.Completed or LoadTestStatus.Failed)
            throw new DomainException($"当前状态不允许取消（{Status}）", "LOADTEST_STATE_INVALID");

        Status = LoadTestStatus.Cancelled;
        FinishedAt = now;
    }

    /// <summary>是否可取消（执行中 / 排队中）</summary>
    public bool CanCancel => Status is LoadTestStatus.Queued or LoadTestStatus.Running;
}

/// <summary>
/// 压测统计结果（值对象）— 引擎执行完毕后一次性回填。
/// </summary>
public sealed record LoadTestStatistics(
    long TotalRequests,
    long SuccessCount,
    long FailCount,
    double Qps,
    double AvgLatencyMs,
    double P50Ms,
    double P95Ms,
    double P99Ms,
    double MaxLatencyMs,
    double ErrorRatePercent);

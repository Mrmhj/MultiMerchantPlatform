using PerformanceService.Domain.Entities;
using PerformanceService.Domain.Enums;

namespace PerformanceService.DTOs;

/// <summary>创建压测任务请求</summary>
/// <param name="Name">任务名称（2-100 字符）</param>
/// <param name="TargetUrl">压测目标 URL（http/https）</param>
/// <param name="HttpMethod">HTTP 方法（GET/POST/PUT/DELETE）</param>
/// <param name="Concurrency">并发数（1-500）</param>
/// <param name="DurationSeconds">持续时间（秒，1-3600）</param>
/// <param name="BodyJson">请求体 JSON（可选，POST/PUT 生效）</param>
/// <param name="HeadersJson">请求头 JSON（可选，格式 {"name":"value"}）</param>
public sealed record CreateLoadTestTaskRequest(
    string Name,
    string TargetUrl,
    string HttpMethod,
    int Concurrency,
    int DurationSeconds,
    string? BodyJson = null,
    string? HeadersJson = null);

/// <summary>更新压测任务请求</summary>
/// <param name="Name">任务名称</param>
/// <param name="TargetUrl">目标 URL</param>
/// <param name="HttpMethod">HTTP 方法</param>
/// <param name="Concurrency">并发数</param>
/// <param name="DurationSeconds">持续时间</param>
/// <param name="BodyJson">请求体 JSON（可选）</param>
/// <param name="HeadersJson">请求头 JSON（可选）</param>
public sealed record UpdateLoadTestTaskRequest(
    string Name,
    string TargetUrl,
    string HttpMethod,
    int Concurrency,
    int DurationSeconds,
    string? BodyJson = null,
    string? HeadersJson = null);

/// <summary>压测任务响应</summary>
/// <param name="Id">任务 ID</param>
/// <param name="Name">任务名称</param>
/// <param name="TargetUrl">目标 URL</param>
/// <param name="HttpMethod">HTTP 方法</param>
/// <param name="Concurrency">并发数</param>
/// <param name="DurationSeconds">持续时间（秒）</param>
/// <param name="BodyJson">请求体 JSON</param>
/// <param name="HeadersJson">请求头 JSON</param>
/// <param name="Enabled">是否启用</param>
/// <param name="CreatedAt">创建时间</param>
/// <param name="UpdatedAt">更新时间</param>
public sealed record LoadTestTaskResponse(
    Guid Id,
    string Name,
    string TargetUrl,
    string HttpMethod,
    int Concurrency,
    int DurationSeconds,
    string? BodyJson,
    string? HeadersJson,
    bool Enabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>压测运行批次响应</summary>
/// <param name="Id">运行批次 ID</param>
/// <param name="TaskId">任务 ID</param>
/// <param name="TaskName">任务名称（快照）</param>
/// <param name="TargetUrl">目标 URL（快照）</param>
/// <param name="Concurrency">并发数（快照）</param>
/// <param name="DurationSeconds">持续时间（快照）</param>
/// <param name="Status">状态（Queued/Running/Completed/Failed/Cancelled）</param>
/// <param name="CreatedAt">创建时间</param>
/// <param name="StartedAt">开始时间</param>
/// <param name="FinishedAt">结束时间</param>
/// <param name="TotalRequests">总请求数</param>
/// <param name="SuccessCount">成功请求数</param>
/// <param name="FailCount">失败请求数</param>
/// <param name="Qps">QPS</param>
/// <param name="AvgLatencyMs">平均延迟（毫秒）</param>
/// <param name="P50Ms">P50 延迟（毫秒）</param>
/// <param name="P95Ms">P95 延迟（毫秒）</param>
/// <param name="P99Ms">P99 延迟（毫秒）</param>
/// <param name="MaxLatencyMs">最大延迟（毫秒）</param>
/// <param name="ErrorRatePercent">错误率（百分比）</param>
/// <param name="ReportPath">HTML 报告相对路径</param>
/// <param name="ErrorMessage">失败原因</param>
public sealed record LoadTestRunResponse(
    Guid Id,
    Guid TaskId,
    string TaskName,
    string TargetUrl,
    int Concurrency,
    int DurationSeconds,
    string Status,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    long TotalRequests,
    long SuccessCount,
    long FailCount,
    double Qps,
    double AvgLatencyMs,
    double P50Ms,
    double P95Ms,
    double P99Ms,
    double MaxLatencyMs,
    double ErrorRatePercent,
    string? ReportPath,
    string? ErrorMessage);

/// <summary>指标快照响应</summary>
/// <param name="ServiceName">服务名</param>
/// <param name="CapturedAt">采样时间</param>
/// <param name="IsUp">是否可达</param>
/// <param name="ResponseMs">响应时间（毫秒）</param>
/// <param name="ManagedMemoryMb">托管内存（MB）</param>
/// <param name="WorkingSetMb">工作集（MB）</param>
/// <param name="CpuPercent">CPU 使用率（百分比）</param>
/// <param name="Gen0GcCount">Gen0 GC 次数</param>
/// <param name="Gen1GcCount">Gen1 GC 次数</param>
/// <param name="Gen2GcCount">Gen2 GC 次数</param>
/// <param name="ThreadPoolAvailable">可用工作线程数</param>
/// <param name="ThreadPoolMax">工作线程上限</param>
public sealed record MetricsSnapshotResponse(
    string ServiceName,
    DateTime CapturedAt,
    bool IsUp,
    double ResponseMs,
    double? ManagedMemoryMb,
    double? WorkingSetMb,
    double? CpuPercent,
    long? Gen0GcCount,
    long? Gen1GcCount,
    long? Gen2GcCount,
    int? ThreadPoolAvailable,
    int? ThreadPoolMax);

/// <summary>告警记录响应</summary>
/// <param name="Id">告警 ID</param>
/// <param name="ServiceName">服务名</param>
/// <param name="MetricType">指标类型</param>
/// <param name="Level">级别（Warning/Critical）</param>
/// <param name="CurrentValue">当前值</param>
/// <param name="Threshold">阈值</param>
/// <param name="Message">告警说明</param>
/// <param name="Status">状态（Open/Resolved）</param>
/// <param name="CreatedAt">创建时间</param>
/// <param name="ResolvedAt">恢复时间</param>
public sealed record AlertResponse(
    Guid Id,
    string ServiceName,
    string MetricType,
    string Level,
    double CurrentValue,
    double Threshold,
    string Message,
    string Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt);

/// <summary>
/// DTO 映射器 — 实体 → 响应 DTO。
/// </summary>
public static class PerformanceMapper
{
    /// <summary>压测任务 → 响应 DTO</summary>
    public static LoadTestTaskResponse ToTaskResponse(LoadTestTask task) => new(
        task.Id, task.Name, task.TargetUrl, task.HttpMethod, task.Concurrency, task.DurationSeconds,
        task.BodyJson, task.HeadersJson, task.Enabled, task.CreatedAt, task.UpdatedAt);

    /// <summary>压测运行批次 → 响应 DTO</summary>
    public static LoadTestRunResponse ToRunResponse(LoadTestRun run) => new(
        run.Id, run.TaskId, run.TaskName, run.TargetUrl, run.Concurrency, run.DurationSeconds,
        run.Status.ToString(), run.CreatedAt, run.StartedAt, run.FinishedAt,
        run.TotalRequests, run.SuccessCount, run.FailCount, run.Qps, run.AvgLatencyMs,
        run.P50Ms, run.P95Ms, run.P99Ms, run.MaxLatencyMs, run.ErrorRatePercent,
        run.ReportPath, run.ErrorMessage);

    /// <summary>指标快照 → 响应 DTO</summary>
    public static MetricsSnapshotResponse ToSnapshotResponse(MetricsSnapshot snapshot) => new(
        snapshot.ServiceName, snapshot.CapturedAt, snapshot.IsUp, snapshot.ResponseMs,
        snapshot.ManagedMemoryMb, snapshot.WorkingSetMb, snapshot.CpuPercent,
        snapshot.Gen0GcCount, snapshot.Gen1GcCount, snapshot.Gen2GcCount,
        snapshot.ThreadPoolAvailable, snapshot.ThreadPoolMax);

    /// <summary>告警记录 → 响应 DTO</summary>
    public static AlertResponse ToAlertResponse(AlertRecord alert) => new(
        alert.Id, alert.ServiceName, alert.MetricType.ToString(), alert.Level.ToString(),
        alert.CurrentValue, alert.Threshold, alert.Message, alert.Status.ToString(),
        alert.CreatedAt, alert.ResolvedAt);
}

using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Entities;

namespace PerformanceService.Domain.Entities;

/// <summary>
/// 指标快照 — 监控采集器对某个目标服务的一次采样结果。
/// 进程级指标（内存/CPU/GC/线程池）仅在目标暴露 /api/metrics 端点时才有值，否则仅记录可用性与响应时间。
/// </summary>
public sealed class MetricsSnapshot : Entity
{
    private MetricsSnapshot() { } // EF Core

    /// <summary>创建指标快照</summary>
    /// <param name="serviceName">服务名（如 order-service）</param>
    /// <param name="capturedAt">采样时间（UTC）</param>
    /// <param name="isUp">服务是否可达</param>
    /// <param name="responseMs">健康探测响应时间（毫秒，失败为 -1）</param>
    /// <param name="managedMemoryMb">托管堆内存（MB，未暴露为 null）</param>
    /// <param name="workingSetMb">进程工作集（MB，未暴露为 null）</param>
    /// <param name="cpuPercent">CPU 使用率（百分比，未暴露为 null）</param>
    /// <param name="gen0GcCount">Gen0 GC 次数（未暴露为 null）</param>
    /// <param name="gen1GcCount">Gen1 GC 次数（未暴露为 null）</param>
    /// <param name="gen2GcCount">Gen2 GC 次数（未暴露为 null）</param>
    /// <param name="threadPoolAvailable">可用工作线程数（未暴露为 null）</param>
    /// <param name="threadPoolMax">工作线程上限（未暴露为 null）</param>
    /// <param name="sourceJson">原始指标 JSON（未暴露为 null）</param>
    [SetsRequiredMembers]
    public MetricsSnapshot(
        string serviceName,
        DateTime capturedAt,
        bool isUp,
        double responseMs,
        double? managedMemoryMb = null,
        double? workingSetMb = null,
        double? cpuPercent = null,
        long? gen0GcCount = null,
        long? gen1GcCount = null,
        long? gen2GcCount = null,
        int? threadPoolAvailable = null,
        int? threadPoolMax = null,
        string? sourceJson = null)
    {
        ServiceName = (serviceName ?? string.Empty).Trim();
        CapturedAt = capturedAt;
        IsUp = isUp;
        ResponseMs = responseMs;
        ManagedMemoryMb = managedMemoryMb;
        WorkingSetMb = workingSetMb;
        CpuPercent = cpuPercent;
        Gen0GcCount = gen0GcCount;
        Gen1GcCount = gen1GcCount;
        Gen2GcCount = gen2GcCount;
        ThreadPoolAvailable = threadPoolAvailable;
        ThreadPoolMax = threadPoolMax;
        SourceJson = sourceJson;
    }

    /// <summary>服务名</summary>
    public string ServiceName { get; private set; } = string.Empty;

    /// <summary>采样时间（UTC）</summary>
    public DateTime CapturedAt { get; private set; }

    /// <summary>服务是否可达</summary>
    public bool IsUp { get; private set; }

    /// <summary>健康探测响应时间（毫秒，失败为 -1）</summary>
    public double ResponseMs { get; private set; }

    /// <summary>托管堆内存（MB）</summary>
    public double? ManagedMemoryMb { get; private set; }

    /// <summary>进程工作集（MB）</summary>
    public double? WorkingSetMb { get; private set; }

    /// <summary>CPU 使用率（百分比 0-100）</summary>
    public double? CpuPercent { get; private set; }

    /// <summary>Gen0 GC 次数</summary>
    public long? Gen0GcCount { get; private set; }

    /// <summary>Gen1 GC 次数</summary>
    public long? Gen1GcCount { get; private set; }

    /// <summary>Gen2 GC 次数</summary>
    public long? Gen2GcCount { get; private set; }

    /// <summary>可用工作线程数</summary>
    public int? ThreadPoolAvailable { get; private set; }

    /// <summary>工作线程上限</summary>
    public int? ThreadPoolMax { get; private set; }

    /// <summary>原始指标 JSON（未暴露为 null）</summary>
    public string? SourceJson { get; private set; }
}

using System.Diagnostics;

namespace PerformanceService.Application.Services;

/// <summary>
/// 进程级指标（一次采样结果）。
/// </summary>
/// <param name="CapturedAt">采样时间（UTC）</param>
/// <param name="ManagedMemoryMb">托管堆内存（MB）</param>
/// <param name="WorkingSetMb">进程工作集（MB）</param>
/// <param name="CpuPercent">CPU 使用率（百分比 0-100，按 CPU 核心归一）</param>
/// <param name="Gen0GcCount">Gen0 GC 次数</param>
/// <param name="Gen1GcCount">Gen1 GC 次数</param>
/// <param name="Gen2GcCount">Gen2 GC 次数</param>
/// <param name="ThreadPoolAvailable">可用工作线程数</param>
/// <param name="ThreadPoolMax">工作线程上限</param>
public sealed record ProcessMetrics(
    DateTime CapturedAt,
    double ManagedMemoryMb,
    double WorkingSetMb,
    double CpuPercent,
    long Gen0GcCount,
    long Gen1GcCount,
    long Gen2GcCount,
    int ThreadPoolAvailable,
    int ThreadPoolMax);

/// <summary>
/// 本进程指标采集器 — 通过 GC / Process / ThreadPool 采集当前进程的内存、CPU、GC 与线程池指标。
/// 被 performance-service 自身的 /api/metrics 端点与监控采集器复用。
/// </summary>
public sealed class ProcessMetricsProvider
{
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly object _sync = new();
    private DateTime _lastCpuSampleAt;
    private TimeSpan _lastCpuTotal;

    /// <summary>首次构造时初始化 CPU 采样基准</summary>
    public ProcessMetricsProvider()
    {
        _lastCpuSampleAt = DateTime.UtcNow;
        _lastCpuTotal = _process.TotalProcessorTime;
    }

    /// <summary>采集当前进程指标快照（单线程安全）</summary>
    /// <returns>进程指标</returns>
    public ProcessMetrics Capture()
    {
        lock (_sync)
        {
            var now = DateTime.UtcNow;

            // CPU 使用率 = (本次 TotalProcessorTime - 上次) / (流逝时间 × 核心数) × 100
            var cpuTotal = _process.TotalProcessorTime;
            var elapsed = (now - _lastCpuSampleAt).TotalSeconds;
            double cpuPercent = 0;
            if (elapsed > 0)
            {
                var cpuDeltaSeconds = (cpuTotal - _lastCpuTotal).TotalSeconds;
                cpuPercent = Math.Clamp(
                    cpuDeltaSeconds / (elapsed * Environment.ProcessorCount) * 100, 0, 100);
            }
            _lastCpuSampleAt = now;
            _lastCpuTotal = cpuTotal;

            _process.Refresh();
            ThreadPool.GetAvailableThreads(out var available, out _);
            ThreadPool.GetMaxThreads(out var max, out _);

            return new ProcessMetrics(
                now,
                ManagedMemoryMb: GC.GetTotalMemory(false) / 1024d / 1024d,
                WorkingSetMb: _process.WorkingSet64 / 1024d / 1024d,
                CpuPercent: cpuPercent,
                Gen0GcCount: GC.CollectionCount(0),
                Gen1GcCount: GC.CollectionCount(1),
                Gen2GcCount: GC.CollectionCount(2),
                ThreadPoolAvailable: available,
                ThreadPoolMax: max);
        }
    }
}

namespace PerformanceService.Infrastructure;

/// <summary>
/// 监控目标配置 — 描述一个待监控微服务的地址与探测路径。
/// </summary>
public sealed class MonitorTarget
{
    /// <summary>服务名（如 order-service）</summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>服务基础地址</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>指标端点路径（目标未暴露时为 null，采集降级为仅健康探测）</summary>
    public string MetricsPath { get; set; } = "/api/metrics";

    /// <summary>健康探测路径（默认根路径）</summary>
    public string HealthPath { get; set; } = "/";

    /// <summary>是否为内部服务（采集时携带 X-Internal-Key，用于校验 /api/metrics）</summary>
    public bool IsInternal { get; set; } = true;
}

/// <summary>
/// 监控配置节（Monitoring）。
/// </summary>
public sealed class MonitoringOptions
{
    /// <summary>采集间隔（秒，默认 15）</summary>
    public int IntervalSeconds { get; set; } = 15;

    /// <summary>是否同时采集本服务（performance-service 自身）指标</summary>
    public bool CollectSelfMetrics { get; set; } = true;

    /// <summary>监控目标列表</summary>
    public List<MonitorTarget> Targets { get; set; } = [];

    /// <summary>告警阈值配置</summary>
    public AlertOptions Alerts { get; set; } = new();
}

/// <summary>
/// 告警阈值配置（Monitoring:Alerts）。
/// </summary>
public sealed class AlertOptions
{
    /// <summary>内存告警阈值（MB）</summary>
    public double MemoryWarningMb { get; set; } = 1024;

    /// <summary>内存严重阈值（MB）</summary>
    public double MemoryCriticalMb { get; set; } = 2048;

    /// <summary>响应时间告警阈值（毫秒）</summary>
    public double ResponseTimeWarningMs { get; set; } = 1000;

    /// <summary>响应时间严重阈值（毫秒）</summary>
    public double ResponseTimeCriticalMs { get; set; } = 3000;

    /// <summary>错误率告警阈值（百分比）</summary>
    public double ErrorRateThresholdPercent { get; set; } = 5;

    /// <summary>连续多少次不可用判定为服务宕机（N 次 × 采集间隔）</summary>
    public int DownThresholdConsecutive { get; set; } = 3;
}

/// <summary>
/// 报告目录配置（Reports）。
/// </summary>
public sealed class ReportOptions
{
    /// <summary>报告输出目录（默认 E:\MultiMerchantPlatform\docs\reports）</summary>
    public string Directory { get; set; } = @"E:\MultiMerchantPlatform\docs\reports";
}

/// <summary>
/// 压测限制配置（LoadTest）。
/// </summary>
public sealed class LoadTestOptions
{
    /// <summary>最大并发数（默认 500）</summary>
    public int MaxConcurrency { get; set; } = 500;

    /// <summary>最大持续时间（秒，默认 3600）</summary>
    public int MaxDurationSeconds { get; set; } = 3600;
}

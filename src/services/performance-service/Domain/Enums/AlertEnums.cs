namespace PerformanceService.Domain.Enums;

/// <summary>
/// 告警级别。
/// </summary>
public enum AlertLevel
{
    /// <summary>一般告警（阈值超限但未达严重线）</summary>
    Warning = 0,

    /// <summary>严重告警（严重线超限 / 服务连续不可用）</summary>
    Critical = 1,
}

/// <summary>
/// 告警状态：Open（待处理）→ Resolved（已恢复 / 手动关闭）。
/// </summary>
public enum AlertStatus
{
    /// <summary>待处理</summary>
    Open = 0,

    /// <summary>已恢复 / 已关闭</summary>
    Resolved = 1,
}

/// <summary>
/// 告警指标类型。
/// </summary>
public enum AlertMetricType
{
    /// <summary>服务不可用</summary>
    ServiceDown = 0,

    /// <summary>响应时间超限</summary>
    ResponseTime = 1,

    /// <summary>内存占用超限</summary>
    Memory = 2,

    /// <summary>错误率超限</summary>
    ErrorRate = 3,
}

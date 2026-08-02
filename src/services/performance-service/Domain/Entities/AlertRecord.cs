using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;
using PerformanceService.Domain.Enums;

namespace PerformanceService.Domain.Entities;

/// <summary>
/// 告警记录 — 监控采集发现异常指标时生成。
/// 状态机：Open（待处理）→ Resolved（指标恢复 / 手动关闭）。
/// </summary>
public sealed class AlertRecord : Entity
{
    private AlertRecord() { } // EF Core

    /// <summary>创建告警记录（初始 Open）</summary>
    /// <param name="serviceName">服务名</param>
    /// <param name="metricType">指标类型</param>
    /// <param name="level">告警级别</param>
    /// <param name="currentValue">当前值</param>
    /// <param name="threshold">触发阈值</param>
    /// <param name="message">告警说明</param>
    /// <param name="now">创建时间（UTC）</param>
    [SetsRequiredMembers]
    public AlertRecord(string serviceName, AlertMetricType metricType, AlertLevel level, double currentValue, double threshold, string message, DateTime now)
    {
        ServiceName = (serviceName ?? string.Empty).Trim();
        MetricType = metricType;
        Level = level;
        CurrentValue = currentValue;
        Threshold = threshold;
        Message = message;
        Status = AlertStatus.Open;
        CreatedAt = now;
    }

    /// <summary>服务名</summary>
    public string ServiceName { get; private set; } = string.Empty;

    /// <summary>指标类型</summary>
    public AlertMetricType MetricType { get; private set; }

    /// <summary>告警级别</summary>
    public AlertLevel Level { get; private set; }

    /// <summary>当前值</summary>
    public double CurrentValue { get; private set; }

    /// <summary>触发阈值</summary>
    public double Threshold { get; private set; }

    /// <summary>告警说明</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>状态</summary>
    public AlertStatus Status { get; private set; }

    /// <summary>恢复时间（未恢复为 null）</summary>
    public DateTime? ResolvedAt { get; private set; }

    /// <summary>关闭告警（Open → Resolved）</summary>
    /// <param name="now">关闭时间（UTC）</param>
    public void Resolve(DateTime now)
    {
        if (Status == AlertStatus.Resolved)
            throw new DomainException("告警已关闭，不能重复操作", "ALERT_STATE_INVALID");

        Status = AlertStatus.Resolved;
        ResolvedAt = now;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PerformanceService.Domain.Entities;
using PerformanceService.Domain.Enums;
using PerformanceService.Infrastructure;
using PerformanceService.Infrastructure.Persistence;

namespace PerformanceService.Application.Services;

/// <summary>
/// 告警评估器 — 依据采集快照与连续宕机计数，对照阈值判断是否生成 / 关闭告警。
/// 规则：
/// 1. 服务连续 N 次不可达（DownThresholdConsecutive）→ ServiceDown 严重告警；恢复后自动关闭。
/// 2. 响应时间超过阈值 → ResponseTime 告警（Warning/Critical）；回落后关闭。
/// 3. 托管内存超过阈值 → Memory 告警（Warning/Critical）；回落后关闭。
/// 通知扩展点：未来接入 notification-service 推送（当前以日志输出 + AlertRecord 落库）。
/// </summary>
public sealed class AlertEvaluator(
    PerformanceDbContext db,
    IOptions<MonitoringOptions> monitoringOptions,
    TimeProvider timeProvider,
    ILogger<AlertEvaluator> logger)
{
    /// <summary>评估一轮快照，生成或关闭告警并落库</summary>
    /// <param name="snapshots">本轮采集快照列表</param>
    /// <param name="consecutiveDownCounts">各服务连续不可达次数</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task EvaluateAsync(
        IReadOnlyList<MetricsSnapshot> snapshots,
        IReadOnlyDictionary<string, int> consecutiveDownCounts,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var options = monitoringOptions.Value.Alerts;

        // 现有 Open 告警按 (服务, 类型) 索引（同键取最新）
        var openAlerts = await db.AlertRecords
            .Where(a => a.Status == AlertStatus.Open)
            .ToListAsync(cancellationToken);
        var openByKey = openAlerts
            .GroupBy(a => (a.ServiceName, a.MetricType))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.CreatedAt).First());

        var changed = false;

        foreach (var snapshot in snapshots)
        {
            if (!snapshot.IsUp)
            {
                // 连续不可达达到阈值 → ServiceDown 告警
                var downCount = consecutiveDownCounts.GetValueOrDefault(snapshot.ServiceName);
                if (downCount >= options.DownThresholdConsecutive)
                {
                    changed |= CreateOrKeep(openByKey, snapshot.ServiceName, AlertMetricType.ServiceDown,
                        AlertLevel.Critical, downCount, options.DownThresholdConsecutive,
                        $"服务连续 {downCount} 次不可达（阈值 {options.DownThresholdConsecutive} 次）", now);
                }
                continue; // 不可达时不评估其它指标
            }

            // 服务恢复 → 关闭 ServiceDown 告警
            changed |= ResolveIfOpen(openByKey, snapshot.ServiceName, AlertMetricType.ServiceDown, now);

            // 响应时间（探测失败 ResponseMs = -1，跳过）
            if (snapshot.ResponseMs >= 0)
            {
                if (snapshot.ResponseMs > options.ResponseTimeCriticalMs)
                {
                    changed |= CreateOrKeep(openByKey, snapshot.ServiceName, AlertMetricType.ResponseTime,
                        AlertLevel.Critical, snapshot.ResponseMs, options.ResponseTimeCriticalMs,
                        $"响应时间 {snapshot.ResponseMs:F0}ms 超过严重阈值 {options.ResponseTimeCriticalMs:F0}ms", now);
                }
                else if (snapshot.ResponseMs > options.ResponseTimeWarningMs)
                {
                    changed |= CreateOrKeep(openByKey, snapshot.ServiceName, AlertMetricType.ResponseTime,
                        AlertLevel.Warning, snapshot.ResponseMs, options.ResponseTimeWarningMs,
                        $"响应时间 {snapshot.ResponseMs:F0}ms 超过告警阈值 {options.ResponseTimeWarningMs:F0}ms", now);
                }
                else
                {
                    changed |= ResolveIfOpen(openByKey, snapshot.ServiceName, AlertMetricType.ResponseTime, now);
                }
            }

            // 托管内存（仅目标暴露 /api/metrics 时有值）
            if (snapshot.ManagedMemoryMb is { } memoryMb)
            {
                if (memoryMb > options.MemoryCriticalMb)
                {
                    changed |= CreateOrKeep(openByKey, snapshot.ServiceName, AlertMetricType.Memory,
                        AlertLevel.Critical, memoryMb, options.MemoryCriticalMb,
                        $"托管内存 {memoryMb:F0}MB 超过严重阈值 {options.MemoryCriticalMb:F0}MB", now);
                }
                else if (memoryMb > options.MemoryWarningMb)
                {
                    changed |= CreateOrKeep(openByKey, snapshot.ServiceName, AlertMetricType.Memory,
                        AlertLevel.Warning, memoryMb, options.MemoryWarningMb,
                        $"托管内存 {memoryMb:F0}MB 超过告警阈值 {options.MemoryWarningMb:F0}MB", now);
                }
                else
                {
                    changed |= ResolveIfOpen(openByKey, snapshot.ServiceName, AlertMetricType.Memory, now);
                }
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>创建告警（同键无 Open 时）或保持现状</summary>
    private bool CreateOrKeep(
        IDictionary<(string, AlertMetricType), AlertRecord> openByKey,
        string serviceName, AlertMetricType metricType, AlertLevel level,
        double currentValue, double threshold, string message, DateTime now)
    {
        var key = (serviceName, metricType);
        if (openByKey.ContainsKey(key))
            return false;

        var alert = new AlertRecord(serviceName, metricType, level, currentValue, threshold, message, now);
        db.AlertRecords.Add(alert);
        openByKey[key] = alert;
        logger.LogWarning("新告警：{Service} {Metric}，当前值 {Current}，阈值 {Threshold}（{Level}）",
            serviceName, metricType, currentValue, threshold, level);
        return true;
    }

    /// <summary>关闭同键 Open 告警（存在时）</summary>
    private bool ResolveIfOpen(
        IDictionary<(string, AlertMetricType), AlertRecord> openByKey,
        string serviceName, AlertMetricType metricType, DateTime now)
    {
        var key = (serviceName, metricType);
        if (!openByKey.TryGetValue(key, out var alert))
            return false;

        alert.Resolve(now);
        openByKey.Remove(key);
        logger.LogInformation("告警恢复：{Service} {Metric} 已回到正常范围", serviceName, metricType);
        return true;
    }
}

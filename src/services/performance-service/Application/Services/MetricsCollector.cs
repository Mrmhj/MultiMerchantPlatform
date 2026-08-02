using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PerformanceService.Domain.Entities;
using PerformanceService.Infrastructure;
using PerformanceService.Infrastructure.Persistence;

namespace PerformanceService.Application.Services;

/// <summary>
/// 监控采集器（后台任务）— 按配置间隔轮询所有监控目标：
/// 1. 健康探测（可达性 + 响应时间）；
/// 2. 尝试拉取目标 /api/metrics 完整指标（内存/CPU/GC/线程池，未暴露则降级为 HTTP 层指标）；
/// 3. 采集本服务（performance-service）自身进程指标作为参照基准；
/// 4. 快照入库后交给 <see cref="AlertEvaluator"/> 评估告警。
/// </summary>
public sealed class MetricsCollector : BackgroundService
{
    private readonly IOptions<MonitoringOptions> _monitoringOptions;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProcessMetricsProvider _processMetricsProvider;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MetricsCollector> _logger;

    // 各服务连续不可达计数（并发安全）
    private readonly ConcurrentDictionary<string, int> _consecutiveDown = new();

    /// <summary>构造监控采集器</summary>
    /// <param name="monitoringOptions">监控配置</param>
    /// <param name="scopeFactory">作用域工厂（解析 DbContext）</param>
    /// <param name="httpClientFactory">HttpClient 工厂（命名客户端 monitor）</param>
    /// <param name="processMetricsProvider">本进程指标采集器</param>
    /// <param name="configuration">应用配置（Internal:Key）</param>
    /// <param name="timeProvider">时间提供器</param>
    /// <param name="logger">日志</param>
    public MetricsCollector(
        IOptions<MonitoringOptions> monitoringOptions,
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ProcessMetricsProvider processMetricsProvider,
        IConfiguration configuration,
        TimeProvider timeProvider,
        ILogger<MetricsCollector> logger)
    {
        _monitoringOptions = monitoringOptions;
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _processMetricsProvider = processMetricsProvider;
        _configuration = configuration;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 启动延迟：等服务完全就绪再开始首轮采集
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "指标采集轮次执行异常");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _monitoringOptions.Value.IntervalSeconds)), stoppingToken);
        }
    }

    /// <summary>执行一轮采集（探活 + 指标 + 入库 + 告警评估）</summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task CollectOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var targets = _monitoringOptions.Value.Targets;
        var snapshots = new ConcurrentBag<MetricsSnapshot>();

        // 并行探测各目标
        await Parallel.ForEachAsync(targets, cancellationToken, async (target, ct) =>
        {
            var snapshot = await ProbeAsync(target, ct);
            snapshots.Add(snapshot);

            // 维护连续不可达计数
            if (snapshot.IsUp)
                _consecutiveDown[target.ServiceName] = 0;
            else
                _consecutiveDown.AddOrUpdate(target.ServiceName, 1, (_, count) => count + 1);
        });

        // 采集本服务自身进程指标（参照基准）
        if (_monitoringOptions.Value.CollectSelfMetrics)
        {
            var self = _processMetricsProvider.Capture();
            snapshots.Add(new MetricsSnapshot(
                "performance-service", self.CapturedAt, isUp: true, responseMs: 0,
                self.ManagedMemoryMb, self.WorkingSetMb, self.CpuPercent,
                self.Gen0GcCount, self.Gen1GcCount, self.Gen2GcCount,
                self.ThreadPoolAvailable, self.ThreadPoolMax));
        }

        var snapshotList = snapshots.ToList();

        // 快照入库
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PerformanceDbContext>();
        db.MetricsSnapshots.AddRange(snapshotList);
        await db.SaveChangesAsync(cancellationToken);

        // 告警评估
        var evaluator = scope.ServiceProvider.GetRequiredService<AlertEvaluator>();
        await evaluator.EvaluateAsync(snapshotList, _consecutiveDown.ToDictionary(kv => kv.Key, kv => kv.Value), cancellationToken);

        _logger.LogDebug("指标采集完成：{Count} 个目标，{UpCount} 个在线", snapshotList.Count, snapshotList.Count(s => s.IsUp));
    }

    /// <summary>探测单个目标（健康检查 + 可选指标拉取）</summary>
    private async Task<MetricsSnapshot> ProbeAsync(MonitorTarget target, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var client = _httpClientFactory.CreateClient("monitor");
        var baseUri = new Uri(target.BaseUrl);
        var healthUri = new Uri(baseUri, target.HealthPath);

        // 1. 健康探测
        var stopwatch = Stopwatch.StartNew();
        bool isUp;
        double responseMs;
        try
        {
            using var response = await client.GetAsync(healthUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            stopwatch.Stop();
            isUp = true;
            responseMs = stopwatch.Elapsed.TotalMilliseconds;
        }
        catch (Exception)
        {
            stopwatch.Stop();
            isUp = false;
            responseMs = -1;
        }

        if (!isUp)
            return new MetricsSnapshot(target.ServiceName, now, isUp: false, responseMs: -1);

        // 2. 尝试拉取完整指标（未暴露则降级）
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, target.MetricsPath));
            if (target.IsInternal)
                request.Headers.TryAddWithoutValidation("X-Internal-Key", _configuration["Internal:Key"] ?? string.Empty);

            using var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                return ParseMetricsJson(target.ServiceName, now, responseMs, json);
            }
        }
        catch (Exception)
        {
            // 降级：仅 HTTP 层指标
        }

        return new MetricsSnapshot(target.ServiceName, now, isUp: true, responseMs);
    }

    /// <summary>解析目标 /api/metrics 的标准指标 JSON（解析失败降级为 HTTP 层指标）</summary>
    private static MetricsSnapshot ParseMetricsJson(string serviceName, DateTime now, double responseMs, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            double? GetDouble(string name) =>
                root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number ? el.GetDouble() : null;
            long? GetLong(string name) =>
                root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number ? el.GetInt64() : null;
            int? GetInt(string name) =>
                root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number ? el.GetInt32() : null;

            return new MetricsSnapshot(
                serviceName, now, isUp: true, responseMs,
                GetDouble("managedMemoryMb"), GetDouble("workingSetMb"), GetDouble("cpuPercent"),
                GetLong("gen0GcCount"), GetLong("gen1GcCount"), GetLong("gen2GcCount"),
                GetInt("threadPoolAvailable"), GetInt("threadPoolMax"),
                json.Length <= 8000 ? json : json[..8000]);
        }
        catch (JsonException)
        {
            return new MetricsSnapshot(serviceName, now, isUp: true, responseMs);
        }
    }
}

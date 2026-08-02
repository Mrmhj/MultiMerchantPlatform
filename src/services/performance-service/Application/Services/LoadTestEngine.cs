using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PerformanceService.Domain.Entities;
using PerformanceService.Domain.Enums;
using PerformanceService.Infrastructure;
using PerformanceService.Infrastructure.Persistence;

namespace PerformanceService.Application.Services;

/// <summary>
/// 压测引擎 — 通过 Channel 队列接收压测运行请求并后台执行。
/// 执行流程：加载运行批次 → 标记 Running → 并发 HTTP 压测（实时统计）→ 生成 HTML 报告 → 回填结果。
/// 支持手动停止（取消对应 CancellationTokenSource）与进程关闭时优雅取消。
/// </summary>
public sealed class LoadTestEngine : BackgroundService
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HtmlReportGenerator _reportGenerator;
    private readonly IOptions<LoadTestOptions> _loadTestOptions;
    private readonly IOptions<ReportOptions> _reportOptions;
    private readonly ILogger<LoadTestEngine> _logger;
    private readonly TimeProvider _timeProvider;

    // 运行中批次 → 取消源（停止功能）
    private readonly Dictionary<Guid, CancellationTokenSource> _activeRuns = [];
    private readonly SemaphoreSlim _activeLock = new(1, 1);

    /// <summary>构造压测引擎</summary>
    /// <param name="scopeFactory">作用域工厂（解析 DbContext）</param>
    /// <param name="httpClientFactory">HttpClient 工厂（命名客户端 loadtest）</param>
    /// <param name="reportGenerator">HTML 报告生成器</param>
    /// <param name="loadTestOptions">压测限制配置</param>
    /// <param name="reportOptions">报告目录配置</param>
    /// <param name="timeProvider">时间提供器</param>
    /// <param name="logger">日志</param>
    public LoadTestEngine(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        HtmlReportGenerator reportGenerator,
        IOptions<LoadTestOptions> loadTestOptions,
        IOptions<ReportOptions> reportOptions,
        TimeProvider timeProvider,
        ILogger<LoadTestEngine> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _reportGenerator = reportGenerator;
        _loadTestOptions = loadTestOptions;
        _reportOptions = reportOptions;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>将压测运行批次加入执行队列</summary>
    /// <param name="runId">运行批次 ID</param>
    public void Enqueue(Guid runId) => _queue.Writer.TryWrite(runId);

    /// <summary>停止指定运行批次（取消执行，最终落库为 Cancelled）</summary>
    /// <param name="runId">运行批次 ID</param>
    /// <returns>是否存在该运行批次</returns>
    public async Task<bool> StopAsync(Guid runId)
    {
        await _activeLock.WaitAsync();
        try
        {
            if (_activeRuns.TryGetValue(runId, out var cts))
            {
                cts.Cancel();
                return true;
            }
            return false;
        }
        finally
        {
            _activeLock.Release();
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var runId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ExecuteRunAsync(runId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("压测引擎停止（进程关闭），排队批次 {RunId} 保留 Queued 状态", runId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "压测运行 {RunId} 执行异常", runId);
                await FailRunAsync(runId, ex.Message);
            }
        }
    }

    private async Task ExecuteRunAsync(Guid runId, CancellationToken stoppingToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var (run, task) = await LoadRunAsync(runId);
        if (run is null)
        {
            _logger.LogWarning("压测运行 {RunId} 不存在，跳过", runId);
            return;
        }

        if (task is { Enabled: false })
        {
            await FailRunAsync(runId, "压测任务已停用");
            return;
        }

        // 取消源：手动停止 + 进程关闭
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        await _activeLock.WaitAsync();
        try
        {
            _activeRuns[runId] = cts;
        }
        finally
        {
            _activeLock.Release();
        }

        try
        {
            run.MarkRunning(now);
            await SaveRunAsync(run);

            _logger.LogInformation("压测开始：{RunId} 任务 {TaskName}，并发 {Concurrency}，时长 {Duration}s，目标 {Target}",
                runId, run.TaskName, run.Concurrency, run.DurationSeconds, run.TargetUrl);

            var stats = await RunLoadAsync(run.TargetUrl, run.HttpMethod ?? "GET",
                run.Concurrency, run.DurationSeconds, run.BodyJson, run.HeadersJson, cts.Token);

            // 取消则标记 Cancelled，不生成报告
            if (cts.IsCancellationRequested)
            {
                run.Cancel(_timeProvider.GetUtcNow().UtcDateTime);
                _logger.LogInformation("压测被取消：{RunId}", runId);
            }
            else
            {
                var reportPath = await _reportGenerator.GenerateAsync(run, stats);
                run.Complete(stats, _timeProvider.GetUtcNow().UtcDateTime, reportPath);
                _logger.LogInformation("压测完成：{RunId} 总请求 {Total}，QPS {Qps:F1}，P95 {P95:F1}ms，错误率 {Error:F2}%",
                    runId, stats.TotalRequests, stats.Qps, stats.P95Ms, stats.ErrorRatePercent);
            }
        }
        catch (OperationCanceledException)
        {
            run.Cancel(_timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "压测执行失败：{RunId}", runId);
            run.Fail(ex.Message, _timeProvider.GetUtcNow().UtcDateTime);
        }
        finally
        {
            await _activeLock.WaitAsync();
            try
            {
                _activeRuns.Remove(runId);
            }
            finally
            {
                _activeLock.Release();
            }

            await SaveRunAsync(run);
        }
    }

    /// <summary>执行并发压测并实时统计</summary>
    /// <param name="targetUrl">目标 URL</param>
    /// <param name="method">HTTP 方法</param>
    /// <param name="concurrency">并发数</param>
    /// <param name="durationSeconds">持续时间（秒）</param>
    /// <param name="bodyJson">请求体（可选）</param>
    /// <param name="headersJson">请求头 JSON（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>统计结果</returns>
    private async Task<LoadTestStatistics> RunLoadAsync(string targetUrl, string method, int concurrency, int durationSeconds, string? bodyJson, string? headersJson, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("loadtest");
        var sw = Stopwatch.StartNew();
        var latencies = new ConcurrentBag<double>();
        long total = 0, success = 0, fail = 0;
        var deadline = TimeSpan.FromSeconds(durationSeconds);

        var headers = ParseHeaders(headersJson);

        // 每个 worker 独立循环发送请求，直到持续时间到期；取消时统一抛出
        var workers = Enumerable.Range(0, concurrency).Select(_ => Task.Run(async () =>
        {
            while (sw.Elapsed < deadline)
            {
                var request = BuildRequest(method, targetUrl, bodyJson, headers);
                var started = sw.Elapsed.TotalMilliseconds;
                try
                {
                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    var elapsedMs = sw.Elapsed.TotalMilliseconds - started;
                    latencies.Add(elapsedMs);
                    Interlocked.Increment(ref total);
                    if (response.IsSuccessStatusCode)
                        Interlocked.Increment(ref success);
                    else
                        Interlocked.Increment(ref fail);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw; // 取消传播到外层
                }
                catch (Exception)
                {
                    var elapsedMs = sw.Elapsed.TotalMilliseconds - started;
                    latencies.Add(elapsedMs);
                    Interlocked.Increment(ref total);
                    Interlocked.Increment(ref fail);
                }
            }
        }, cancellationToken)).ToArray();

        try
        {
            await Task.WhenAll(workers);
        }
        catch (OperationCanceledException)
        {
            throw; // 传播取消（手动停止 / 进程关闭）
        }

        var actualSeconds = sw.Elapsed.TotalSeconds;
        return ComputeStatistics(total, success, fail, actualSeconds, latencies);
    }

    /// <summary>汇总统计结果（QPS / 平均 / P50 / P95 / P99 / 最大 / 错误率）</summary>
    private static LoadTestStatistics ComputeStatistics(long total, long success, long fail, double elapsedSeconds, IEnumerable<double> latencies)
    {
        var ordered = latencies.Order().ToArray();
        double p(int percentile) => ordered.Length == 0
            ? 0
            : ordered[(int)Math.Min(ordered.Length - 1, ordered.Length * percentile / 100.0)];

        return new LoadTestStatistics(
            TotalRequests: total,
            SuccessCount: success,
            FailCount: fail,
            Qps: elapsedSeconds > 0 ? total / elapsedSeconds : 0,
            AvgLatencyMs: ordered.Length == 0 ? 0 : ordered.Average(),
            P50Ms: p(50),
            P95Ms: p(95),
            P99Ms: p(99),
            MaxLatencyMs: ordered.Length == 0 ? 0 : ordered[^1],
            ErrorRatePercent: total > 0 ? fail * 100.0 / total : 0);
    }

    /// <summary>构建单个 HTTP 请求消息</summary>
    private static HttpRequestMessage BuildRequest(string method, string targetUrl, string? bodyJson, IReadOnlyDictionary<string, string>? headers)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), targetUrl);
        if (bodyJson is { Length: > 0 } && method is "POST" or "PUT")
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        if (headers is not null)
        {
            foreach (var (key, value) in headers)
            {
                if (!request.Headers.TryAddWithoutValidation(key, value))
                    request.Content?.Headers.TryAddWithoutValidation(key, value);
            }
        }
        return request;
    }

    /// <summary>解析请求头 JSON（{"name":"value"}）</summary>
    private static IReadOnlyDictionary<string, string>? ParseHeaders(string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson))
            return null;

        try
        {
            var node = JsonNode.Parse(headersJson)?.AsObject();
            var result = new Dictionary<string, string>();
            foreach (var (key, value) in node ?? [])
                result[key] = value?.GetValue<string>() ?? string.Empty;
            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<(LoadTestRun? Run, LoadTestTask? Task)> LoadRunAsync(Guid runId)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PerformanceDbContext>();
        var run = await db.LoadTestRuns.FirstOrDefaultAsync(r => r.Id == runId);
        var task = run is null ? null : await db.LoadTestTasks.FirstOrDefaultAsync(t => t.Id == run.TaskId);
        return (run, task);
    }

    private async Task SaveRunAsync(LoadTestRun run)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PerformanceDbContext>();
        db.LoadTestRuns.Update(run);
        await db.SaveChangesAsync();
    }

    private async Task FailRunAsync(Guid runId, string reason)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PerformanceDbContext>();
        var run = await db.LoadTestRuns.FirstOrDefaultAsync(r => r.Id == runId);
        if (run is null)
            return;
        run.Fail(reason, _timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync();
    }
}

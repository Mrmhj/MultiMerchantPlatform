using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Core.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace BuildingBlocks.Communication;

/// <summary>
/// HTTP 服务客户端实现（Strategy 模式 — HTTP 策略）。
/// 内置 Polly v8 弹性管线：重试（指数退避+抖动）→ 熔断 → 超时，配置节化（Resilience，可选）。
/// </summary>
public class HttpServiceClient(HttpClient httpClient, IOptions<ResilienceOptions>? options = null) : IServiceClient
{
    private readonly HttpClient _httpClient = httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline = BuildPipeline(options?.Value ?? new ResilienceOptions());

    /// <summary>构建弹性管线：重试（瞬时故障/5xx/408/429）→ 熔断（连续失败）→ 每次尝试超时</summary>
    private static ResiliencePipeline<HttpResponseMessage> BuildPipeline(ResilienceOptions o)
    {
        return new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = o.MaxRetryAttempts,
                Delay = TimeSpan.FromMilliseconds(o.RetryBaseDelayMs),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => IsTransient(r.StatusCode)),
                // 被判定「需重试」的失败响应由策略丢弃 → 手动释放防连接泄漏
                OnRetry = args =>
                {
                    args.Outcome.Result?.Dispose();
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = o.CircuitBreakerFailureRatio,
                MinimumThroughput = o.CircuitBreakerMinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(o.CircuitBreakerSamplingSeconds),
                BreakDuration = TimeSpan.FromSeconds(o.CircuitBreakerBreakSeconds),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => IsTransient(r.StatusCode))
            })
            .AddTimeout(TimeSpan.FromSeconds(o.TimeoutSeconds))
            .Build();
    }

    /// <summary>瞬时故障判定：408 请求超时 / 429 限流 / 5xx 服务端错误（重试有意义）；4xx 不重试</summary>
    private static bool IsTransient(HttpStatusCode code)
        => code == HttpStatusCode.RequestTimeout
        || code == HttpStatusCode.TooManyRequests
        || (int)code >= 500;

    public Task<Result<T>> GetAsync<T>(string path, CancellationToken ct = default)
        => SendAsync<T>(ct => _httpClient.GetAsync(path, ct), ct);

    public Task<Result<T>> PostAsync<T>(string path, object body, CancellationToken ct = default)
        => SendAsync<T>(ct => _httpClient.PostAsJsonAsync(path, body, JsonOptions, ct), ct);

    public Task<Result<T>> PutAsync<T>(string path, object body, CancellationToken ct = default)
        => SendAsync<T>(ct => _httpClient.PutAsJsonAsync(path, body, JsonOptions, ct), ct);

    public Task<Result<T>> DeleteAsync<T>(string path, CancellationToken ct = default)
        => SendAsync<T>(ct => _httpClient.DeleteAsync(path, ct), ct);

    /// <summary>经弹性管线发送请求并转 Result（熔断/超时/网络异常 → 业务失败结果）</summary>
    private async Task<Result<T>> SendAsync<T>(Func<CancellationToken, Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        try
        {
            using var response = await _pipeline.ExecuteAsync(async token => await send(token), ct);
            return await HandleResponseAsync<T>(response, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // 策略超时（非客户端取消）→ 视为失败，不向上传播取消
            return Result.Failure<T>("服务调用超时", "Timeout");
        }
        catch (BrokenCircuitException)
        {
            return Result.Failure<T>("服务暂时不可用（熔断开启）", "CircuitBroken");
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<T>($"服务调用网络错误: {ex.Message}", "NetworkError");
        }
    }

    private static async Task<Result<T>> HandleResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return Result.Failure<T>($"HTTP {response.StatusCode}: {error}", response.StatusCode.ToString());
        }

        var content = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        return Result.Success(content!);
    }
}

/// <summary>
/// 服务间调用弹性配置（Polly v8）。默认值开箱即用；可通过配置节 <c>Resilience</c> 覆盖。
/// </summary>
public sealed class ResilienceOptions
{
    /// <summary>最大重试次数（不含首次请求）</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>重试基础延迟（毫秒），指数退避：base, base*2, base*4…</summary>
    public int RetryBaseDelayMs { get; set; } = 200;

    /// <summary>熔断：失败率阈值（0-1），达到则打开熔断</summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>熔断：统计窗口内最小请求数（不足不触发熔断）</summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 8;

    /// <summary>熔断：统计采样窗口（秒）</summary>
    public int CircuitBreakerSamplingSeconds { get; set; } = 10;

    /// <summary>熔断：开启持续时间（秒），到期进入半开探测</summary>
    public int CircuitBreakerBreakSeconds { get; set; } = 15;

    /// <summary>单次尝试超时（秒）</summary>
    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// 通信模块 DI 注册扩展。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册服务客户端 — 按服务名注册，支持 HTTP/gRPC 切换（Strategy 模式）。
    /// HTTP 客户端自动带 Polly 弹性管线（重试/熔断/超时）。
    /// </summary>
    public static IServiceCollection AddServiceClient(
        this IServiceCollection services,
        string serviceName,
        string baseUrl,
        CommunicationProtocol protocol = CommunicationProtocol.Http)
    {
        if (protocol == CommunicationProtocol.Http)
        {
            services.AddHttpClient<IServiceClient, HttpServiceClient>(serviceName, client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });
        }
        else
        {
            // gRPC 策略（JSON-gRPC 模式）：单例封装 GrpcChannel
            services.AddSingleton<IServiceClient>(_ => new GrpcServiceClient(baseUrl));
        }

        return services;
    }

    /// <summary>
    /// 绑定服务间调用弹性配置（配置节 <c>Resilience</c>）。不调用则使用 <see cref="ResilienceOptions"/> 默认值。
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置（读取 "Resilience" 节）</param>
    /// <returns>服务集合（链式调用）</returns>
    public static IServiceCollection AddServiceClientResilience(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ResilienceOptions>(configuration.GetSection("Resilience"));
        return services;
    }
}

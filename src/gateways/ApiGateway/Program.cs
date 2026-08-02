using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// YARP 反向代理网关
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transforms =>
    {
        transforms.AddOriginalHost();
    });

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ─────────────────────────────────────────────────────────────
// 入口限流（.NET 内置 RateLimiter，零外部依赖）
// 三层链式：全局并发 → 固定窗口（普通 API）→ 令牌桶（秒杀等突发接口）
// 配置节：RateLimit（见 appsettings.json / appsettings.Example.json）
// ─────────────────────────────────────────────────────────────
var rateLimit = builder.Configuration.GetSection("RateLimit");
var concurrency = rateLimit.GetSection("Concurrency").Get<RateLimitConcurrencyOptions>() ?? new();
var globalWindow = rateLimit.GetSection("FixedWindow").Get<RateLimitFixedWindowOptions>() ?? new();
var seckillBucket = rateLimit.GetSection("TokenBucket").Get<RateLimitTokenBucketOptions>() ?? new();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // 链式限流器：并发（全局限流）+ 分区窗口（普通/秒杀分流）
    // 注：.NET 10 起 Create 仅接收返回 RateLimitPartition 的 partitioner 函数
    options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        // 第一层：全局并发控制（防连接打满）
        PartitionedRateLimiter.Create<HttpContext, string>(_ =>
            RateLimitPartition.GetConcurrencyLimiter("global", _ => new ConcurrencyLimiterOptions
            {
                PermitLimit = concurrency.PermitLimit,
                QueueLimit = concurrency.QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            })),
        // 第二层：按分区限流 —— 秒杀接口走令牌桶（允许突发），其余走固定窗口（按 IP）
        PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        {
            var path = ctx.Request.Path.Value ?? string.Empty;
            // 秒杀/压测类突发接口：令牌桶（以全局限额计，不按 IP，避免多租户相互影响）
            if (path.Contains("/api/promotion/seckills/buy", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/api/performance/benchmarks", StringComparison.OrdinalIgnoreCase))
            {
                return RateLimitPartition.GetTokenBucketLimiter("seckill-bucket", _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = seckillBucket.TokenLimit,
                    TokensPerPeriod = seckillBucket.TokensPerPeriod,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(seckillBucket.ReplenishmentPeriodSeconds),
                    QueueLimit = seckillBucket.QueueLimit,
                    AutoReplenishment = true
                });
            }

            // 其余 API：固定窗口（按客户端 IP）
            var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter($"fixed:{ip}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = globalWindow.PermitLimit,
                Window = TimeSpan.FromSeconds(globalWindow.WindowSeconds),
                QueueLimit = globalWindow.QueueLimit,
                AutoReplenishment = true
            });
        }));

    // 429 响应：统一 JSON + Retry-After（队列满时直接拒绝，避免请求悬挂）
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString("0");
        }
        await context.HttpContext.Response.WriteAsync(
            "{\"code\":429,\"message\":\"请求过于频繁，请稍后重试\"}", ct);
    };
});

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseRateLimiter();

// 健康检查（限流策略内：固定窗口）
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// YARP 代理端点
app.MapReverseProxy();

app.Run();

/// <summary>RateLimit:Concurrency 配置（全局并发控制）</summary>
public sealed class RateLimitConcurrencyOptions
{
    /// <summary>最大并发许可数</summary>
    public int PermitLimit { get; set; } = 500;
    /// <summary>排队上限</summary>
    public int QueueLimit { get; set; } = 200;
}

/// <summary>RateLimit:FixedWindow 配置（普通 API 按 IP 固定窗口）</summary>
public sealed class RateLimitFixedWindowOptions
{
    /// <summary>窗口内许可数</summary>
    public int PermitLimit { get; set; } = 120;
    /// <summary>窗口时长（秒）</summary>
    public int WindowSeconds { get; set; } = 60;
    /// <summary>排队上限</summary>
    public int QueueLimit { get; set; } = 20;
}

/// <summary>RateLimit:TokenBucket 配置（秒杀/压测突发接口）</summary>
public sealed class RateLimitTokenBucketOptions
{
    /// <summary>桶容量（最大突发量）</summary>
    public int TokenLimit { get; set; } = 2000;
    /// <summary>每周期补充令牌数</summary>
    public int TokensPerPeriod { get; set; } = 1000;
    /// <summary>补充周期（秒）</summary>
    public int ReplenishmentPeriodSeconds { get; set; } = 60;
    /// <summary>排队上限</summary>
    public int QueueLimit { get; set; } = 50;
}

namespace MessagingService.Application.Options;

/// <summary>
/// 消息队列服务配置。
/// </summary>
public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    /// <summary>后台分发器轮询间隔（秒）</summary>
    public int PollIntervalSeconds { get; set; } = 5;

    /// <summary>每批处理的消息数</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>默认最大重试次数（超过转死信）</summary>
    public int DefaultMaxRetryCount { get; set; } = 5;

    /// <summary>指数退避基准间隔（秒），第 N 次重试间隔 = base × 2^(N-1)</summary>
    public int RetryBaseIntervalSeconds { get; set; } = 5;

    /// <summary>重试最大间隔（秒），防止退避无限增长</summary>
    public int MaxRetryDelaySeconds { get; set; } = 300;

    /// <summary>投递请求超时（秒）</summary>
    public int HttpClientTimeoutSeconds { get; set; } = 30;
}

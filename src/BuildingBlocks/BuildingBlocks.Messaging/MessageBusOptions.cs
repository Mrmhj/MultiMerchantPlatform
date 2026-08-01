namespace BuildingBlocks.Messaging;

/// <summary>
/// 消息总线配置 — 用于 HTTP 发布器连接 messaging-service。
/// </summary>
public sealed class MessageBusOptions
{
    public const string SectionName = "MessageBus";

    /// <summary>messaging-service 基地址（如 http://localhost:8010）</summary>
    public string BaseUrl { get; set; } = "http://localhost:8010";

    /// <summary>发布请求超时（秒）</summary>
    public int TimeoutSeconds { get; set; } = 10;
}

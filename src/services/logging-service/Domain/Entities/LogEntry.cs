namespace LoggingService.Domain.Entities;

/// <summary>
/// 日志条目（持久化实体）— 由各微服务通过 BuildingBlocks.Logging 上报。
/// </summary>
public sealed class LogEntry
{
    private LogEntry() { } // EF Core

    /// <summary>创建日志条目（客户端上报时构建）</summary>
    /// <param name="id">日志 ID</param>
    /// <param name="serviceName">来源服务名</param>
    /// <param name="level">日志级别</param>
    /// <param name="message">日志消息</param>
    /// <param name="exception">异常堆栈（可选）</param>
    /// <param name="traceId">链路追踪 ID（可选）</param>
    /// <param name="spanId">Span ID（可选）</param>
    /// <param name="category">日志分类（可选）</param>
    /// <param name="propertiesJson">附加属性 JSON（可选）</param>
    /// <param name="timestamp">日志产生时间（缺省当前时间）</param>
    public LogEntry(
        Guid id,
        string serviceName,
        string level,
        string message,
        string? exception = null,
        string? traceId = null,
        string? spanId = null,
        string? category = null,
        string? propertiesJson = null,
        DateTime? timestamp = null)
    {
        Id = id;
        ServiceName = serviceName;
        Level = level;
        Message = message;
        Exception = exception;
        TraceId = traceId;
        SpanId = spanId;
        Category = category;
        PropertiesJson = propertiesJson;
        Timestamp = timestamp ?? DateTime.UtcNow;
    }

    /// <summary>日志 ID</summary>
    public Guid Id { get; private set; }

    /// <summary>来源服务名（如 order-service）</summary>
    public string ServiceName { get; private set; } = null!;

    /// <summary>日志级别（Trace/Debug/Information/Warning/Error/Critical）</summary>
    public string Level { get; private set; } = null!;

    /// <summary>日志消息</summary>
    public string Message { get; private set; } = null!;

    /// <summary>异常堆栈</summary>
    public string? Exception { get; private set; }

    /// <summary>链路追踪 ID</summary>
    public string? TraceId { get; private set; }

    /// <summary>Span ID</summary>
    public string? SpanId { get; private set; }

    /// <summary>日志分类（Logger 类别名）</summary>
    public string? Category { get; private set; }

    /// <summary>附加属性（JSON 序列化）</summary>
    public string? PropertiesJson { get; private set; }

    /// <summary>日志产生时间</summary>
    public DateTime Timestamp { get; private set; }
}

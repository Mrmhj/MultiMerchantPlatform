namespace BuildingBlocks.Logging;

/// <summary>
/// 日志条目 — 统一日志模型。
/// </summary>
public class LogEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string ServiceName { get; init; } = string.Empty;
    public string Level { get; init; } = "Information";
    public string Message { get; init; } = string.Empty;
    public string? Exception { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? Category { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Dictionary<string, object?> Properties { get; init; } = new();
}

/// <summary>
/// 日志查询请求。
/// </summary>
public class LogQuery
{
    public string? ServiceName { get; set; }
    public string? Level { get; set; }
    public string? Keyword { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

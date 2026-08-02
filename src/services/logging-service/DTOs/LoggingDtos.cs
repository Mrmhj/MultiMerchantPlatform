using System.ComponentModel.DataAnnotations;

namespace LoggingService.DTOs;

/// <summary>
/// 日志条目 DTO — 与 BuildingBlocks.Logging.LogEntry 契约一致（客户端上报格式）。
/// </summary>
public sealed record LogEntryDto
{
    /// <summary>日志 ID（客户端生成）</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>来源服务名（如 order-service）</summary>
    [Required, StringLength(100)]
    public required string ServiceName { get; init; }

    /// <summary>日志级别（Trace/Debug/Information/Warning/Error/Critical）</summary>
    [Required, StringLength(20)]
    public required string Level { get; init; }

    /// <summary>日志消息</summary>
    [Required]
    public required string Message { get; init; }

    /// <summary>异常堆栈（可选）</summary>
    public string? Exception { get; init; }

    /// <summary>链路追踪 ID（可选）</summary>
    public string? TraceId { get; init; }

    /// <summary>Span ID（可选）</summary>
    public string? SpanId { get; init; }

    /// <summary>日志分类（Logger 类别名，可选）</summary>
    [StringLength(200)]
    public string? Category { get; init; }

    /// <summary>日志产生时间</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>附加属性（客户端为对象字典，落库时序列化为 JSON）</summary>
    public Dictionary<string, object?>? Properties { get; init; }
}

/// <summary>日志查询 DTO</summary>
public sealed record LogQueryDto
{
    /// <summary>按服务名精确过滤（可选）</summary>
    [StringLength(100)]
    public string? ServiceName { get; init; }

    /// <summary>按级别精确过滤（可选）</summary>
    [StringLength(20)]
    public string? Level { get; init; }

    /// <summary>按消息关键字模糊过滤（可选）</summary>
    public string? Keyword { get; init; }

    /// <summary>起始时间（含，可选）</summary>
    public DateTime? From { get; init; }

    /// <summary>结束时间（含，可选）</summary>
    public DateTime? To { get; init; }

    /// <summary>页码（默认 1）</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数（默认 50，上限 200）</summary>
    public int PageSize { get; init; } = 50;
}

/// <summary>日志响应 DTO</summary>
public sealed record LogResponse
{
    /// <summary>日志 ID</summary>
    public Guid Id { get; init; }

    /// <summary>来源服务名</summary>
    public required string ServiceName { get; init; }

    /// <summary>日志级别</summary>
    public required string Level { get; init; }

    /// <summary>日志消息</summary>
    public required string Message { get; init; }

    /// <summary>异常堆栈</summary>
    public string? Exception { get; init; }

    /// <summary>链路追踪 ID</summary>
    public string? TraceId { get; init; }

    /// <summary>Span ID</summary>
    public string? SpanId { get; init; }

    /// <summary>日志分类</summary>
    public string? Category { get; init; }

    /// <summary>附加属性（JSON 字符串）</summary>
    public string? PropertiesJson { get; init; }

    /// <summary>日志产生时间</summary>
    public DateTime Timestamp { get; init; }
}

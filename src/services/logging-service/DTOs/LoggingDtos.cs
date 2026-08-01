using System.ComponentModel.DataAnnotations;

namespace LoggingService.DTOs;

/// <summary>
/// 日志条目 DTO — 与 BuildingBlocks.Logging.LogEntry 契约一致（客户端上报格式）。
/// </summary>
public sealed record LogEntryDto
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [Required, StringLength(100)]
    public required string ServiceName { get; init; }

    [Required, StringLength(20)]
    public required string Level { get; init; }

    [Required]
    public required string Message { get; init; }

    public string? Exception { get; init; }

    public string? TraceId { get; init; }

    public string? SpanId { get; init; }

    [StringLength(200)]
    public string? Category { get; init; }

    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>附加属性（客户端为对象字典，落库时序列化为 JSON）</summary>
    public Dictionary<string, object?>? Properties { get; init; }
}

/// <summary>日志查询 DTO</summary>
public sealed record LogQueryDto
{
    [StringLength(100)]
    public string? ServiceName { get; init; }

    [StringLength(20)]
    public string? Level { get; init; }

    public string? Keyword { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}

/// <summary>日志响应 DTO</summary>
public sealed record LogResponse
{
    public Guid Id { get; init; }
    public required string ServiceName { get; init; }
    public required string Level { get; init; }
    public required string Message { get; init; }
    public string? Exception { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? Category { get; init; }
    public string? PropertiesJson { get; init; }
    public DateTime Timestamp { get; init; }
}

using System.Text.Json;
using LoggingService.Domain.Entities;
using LoggingService.DTOs;
using LoggingService.Infrastructure.Persistence;

namespace LoggingService.Application;

/// <summary>
/// 日志写入服务 — 接收客户端批量上报并落库。
/// 开发版使用 EF AddRange 批量插入；高吞吐场景可替换为 SqlBulkCopy（见模块文档扩展点）。
/// </summary>
public sealed class LogIngestService(LoggingDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>批量写入日志</summary>
    public async Task<int> IngestAsync(IEnumerable<LogEntryDto> entries, CancellationToken ct = default)
    {
        var list = entries.ToList();
        if (list.Count == 0)
            return 0;

        var entities = list.Select(e => new LogEntry(
            e.Id,
            e.ServiceName.Trim(),
            e.Level.Trim(),
            e.Message,
            e.Exception,
            e.TraceId,
            e.SpanId,
            e.Category,
            e.Properties is { Count: > 0 } ? JsonSerializer.Serialize(e.Properties, JsonOptions) : null,
            e.Timestamp));

        db.Logs.AddRange(entities);
        await db.SaveChangesAsync(ct);
        return list.Count;
    }
}

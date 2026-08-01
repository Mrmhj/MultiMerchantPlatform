using BuildingBlocks.Core.Results;
using LoggingService.DTOs;
using LoggingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoggingService.Application;

/// <summary>
/// 日志查询服务 — 分页检索（按服务 / 级别 / 关键字 / 时间范围）。
/// </summary>
public sealed class LogQueryService(LoggingDbContext db)
{
    public async Task<PagedResult<LogResponse>> QueryAsync(LogQueryDto query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var q = db.Logs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.ServiceName))
            q = q.Where(l => l.ServiceName == query.ServiceName.Trim());
        if (!string.IsNullOrWhiteSpace(query.Level))
            q = q.Where(l => l.Level == query.Level.Trim());
        if (!string.IsNullOrWhiteSpace(query.Keyword))
            q = q.Where(l => l.Message.Contains(query.Keyword.Trim()));
        if (query.From.HasValue)
            q = q.Where(l => l.Timestamp >= query.From.Value);
        if (query.To.HasValue)
            q = q.Where(l => l.Timestamp <= query.To.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(l => l.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<LogResponse>(
            items.Select(ToResponse).ToList(), total, page, pageSize);
    }

    public async Task<LogResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await db.Logs.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct);
        return entity is null ? null : ToResponse(entity);
    }

    private static LogResponse ToResponse(Domain.Entities.LogEntry e) => new()
    {
        Id = e.Id,
        ServiceName = e.ServiceName,
        Level = e.Level,
        Message = e.Message,
        Exception = e.Exception,
        TraceId = e.TraceId,
        SpanId = e.SpanId,
        Category = e.Category,
        PropertiesJson = e.PropertiesJson,
        Timestamp = e.Timestamp,
    };
}

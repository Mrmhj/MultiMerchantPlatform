using LoggingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoggingService.Application;

/// <summary>级别分布统计项</summary>
public sealed record LevelDistribution(string Level, long Count);

/// <summary>Top 服务统计项</summary>
public sealed record TopService(string ServiceName, long Count);

/// <summary>时间趋势点（按小时/天聚合）</summary>
public sealed record TrendPoint(DateTime Bucket, long Count);

/// <summary>
/// 日志统计服务 — 级别分布 / Top 服务 / 时间趋势。
/// </summary>
public sealed class LogStatsService(LoggingDbContext db)
{
    /// <summary>按级别聚合（某时间范围内）</summary>
    public async Task<IReadOnlyList<LevelDistribution>> GetLevelDistributionAsync(
        DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var q = ApplyRange(db.Logs.AsNoTracking(), from, to);
        var rows = await q
            .GroupBy(l => l.Level)
            .Select(g => new { Level = g.Key, Count = g.LongCount() })
            .ToListAsync(ct);

        return rows
            .Select(r => new LevelDistribution(r.Level, r.Count))
            .OrderByDescending(x => x.Count)
            .ToList();
    }

    /// <summary>日志量 Top N 服务</summary>
    public async Task<IReadOnlyList<TopService>> GetTopServicesAsync(
        int top = 10, DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var q = ApplyRange(db.Logs.AsNoTracking(), from, to);
        var rows = await q
            .GroupBy(l => l.ServiceName)
            .Select(g => new { ServiceName = g.Key, Count = g.LongCount() })
            .OrderByDescending(x => x.Count)
            .Take(top)
            .ToListAsync(ct);

        return rows
            .Select(r => new TopService(r.ServiceName, r.Count))
            .ToList();
    }

    /// <summary>
    /// 时间趋势（按天或按小时聚合）。
    /// </summary>
    public async Task<IReadOnlyList<TrendPoint>> GetTrendAsync(
        DateTime? from = null, DateTime? to = null, string granularity = "hour", CancellationToken ct = default)
    {
        var effectiveFrom = from ?? DateTime.UtcNow.AddHours(-24);
        var effectiveTo = to ?? DateTime.UtcNow;
        var q = db.Logs.AsNoTracking()
            .Where(l => l.Timestamp >= effectiveFrom && l.Timestamp <= effectiveTo);

        if (granularity == "day")
        {
            var rows = await q
                .GroupBy(l => new { l.Timestamp.Year, l.Timestamp.Month, l.Timestamp.Day })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.LongCount() })
                .ToListAsync(ct);

            return rows
                .Select(r => new TrendPoint(new DateTime(r.Year, r.Month, r.Day), r.Count))
                .OrderBy(x => x.Bucket)
                .ToList();
        }

        var hourly = await q
            .GroupBy(l => new { l.Timestamp.Year, l.Timestamp.Month, l.Timestamp.Day, l.Timestamp.Hour })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Count = g.LongCount() })
            .ToListAsync(ct);

        return hourly
            .Select(r => new TrendPoint(new DateTime(r.Year, r.Month, r.Day, r.Hour, 0, 0), r.Count))
            .OrderBy(x => x.Bucket)
            .ToList();
    }

    /// <summary>错误日志占比（Error + Critical）</summary>
    public async Task<double> GetErrorRateAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var q = ApplyRange(db.Logs.AsNoTracking(), from, to);
        var total = await q.LongCountAsync(ct);
        if (total == 0)
            return 0;

        var errors = await q
            .Where(l => l.Level == "Error" || l.Level == "Critical")
            .LongCountAsync(ct);
        return Math.Round(errors / (double)total * 100, 2);
    }

    private static IQueryable<Domain.Entities.LogEntry> ApplyRange(
        IQueryable<Domain.Entities.LogEntry> q, DateTime? from, DateTime? to)
    {
        if (from.HasValue)
            q = q.Where(l => l.Timestamp >= from.Value);
        if (to.HasValue)
            q = q.Where(l => l.Timestamp <= to.Value);
        return q;
    }
}

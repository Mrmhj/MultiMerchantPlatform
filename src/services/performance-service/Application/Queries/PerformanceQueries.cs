using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using Microsoft.EntityFrameworkCore;
using PerformanceService.Domain.Entities;
using PerformanceService.Domain.Enums;
using PerformanceService.DTOs;
using PerformanceService.Infrastructure.Persistence;

namespace PerformanceService.Application.Queries;

/// <summary>压测任务列表查询</summary>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
public sealed record LoadTestTaskListQuery(int Page = 1, int PageSize = 20) : IQuery<PagedResult<LoadTestTaskResponse>>;

/// <summary>压测任务列表查询处理器</summary>
public sealed class LoadTestTaskListQueryHandler(PerformanceDbContext db)
    : IQueryHandler<LoadTestTaskListQuery, PagedResult<LoadTestTaskResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<LoadTestTaskResponse>> HandleAsync(LoadTestTaskListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var total = await db.LoadTestTasks.CountAsync(ct);
        var items = await db.LoadTestTasks
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<LoadTestTaskResponse>(items.Select(PerformanceMapper.ToTaskResponse).ToList(), total, page, pageSize);
    }
}

/// <summary>压测运行批次列表查询</summary>
/// <param name="TaskId">任务 ID（可选过滤）</param>
/// <param name="Status">状态过滤（可选：Queued/Running/Completed/Failed/Cancelled）</param>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
public sealed record LoadTestRunListQuery(Guid? TaskId = null, string? Status = null, int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<LoadTestRunResponse>>;

/// <summary>压测运行批次列表查询处理器</summary>
public sealed class LoadTestRunListQueryHandler(PerformanceDbContext db)
    : IQueryHandler<LoadTestRunListQuery, PagedResult<LoadTestRunResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<LoadTestRunResponse>> HandleAsync(LoadTestRunListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var baseQuery = db.LoadTestRuns.AsNoTracking();
        if (query.TaskId is not null)
            baseQuery = baseQuery.Where(r => r.TaskId == query.TaskId);
        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<LoadTestStatus>(query.Status, ignoreCase: true, out var status))
            baseQuery = baseQuery.Where(r => r.Status == status);

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<LoadTestRunResponse>(items.Select(PerformanceMapper.ToRunResponse).ToList(), total, page, pageSize);
    }
}

/// <summary>压测运行批次详情查询</summary>
/// <param name="RunId">运行批次 ID</param>
public sealed record LoadTestRunDetailQuery(Guid RunId) : IQuery<LoadTestRunResponse>;

/// <summary>压测运行批次详情查询处理器</summary>
public sealed class LoadTestRunDetailQueryHandler(PerformanceDbContext db)
    : IQueryHandler<LoadTestRunDetailQuery, LoadTestRunResponse>
{
    /// <inheritdoc />
    public async Task<LoadTestRunResponse> HandleAsync(LoadTestRunDetailQuery query, CancellationToken ct = default)
    {
        var run = await db.LoadTestRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == query.RunId, ct)
            ?? throw new NotFoundException("压测运行批次", query.RunId);
        return PerformanceMapper.ToRunResponse(run);
    }
}

/// <summary>指标快照查询（最新一轮 / 指定服务）</summary>
/// <param name="ServiceName">服务名（可选，缺省返回全部服务最新快照）</param>
public sealed record MetricsLatestQuery(string? ServiceName = null) : IQuery<List<MetricsSnapshotResponse>>;

/// <summary>指标快照查询处理器：返回每个服务最新一次采样（按服务分组取最新）</summary>
public sealed class MetricsLatestQueryHandler(PerformanceDbContext db)
    : IQueryHandler<MetricsLatestQuery, List<MetricsSnapshotResponse>>
{
    /// <inheritdoc />
    public async Task<List<MetricsSnapshotResponse>> HandleAsync(MetricsLatestQuery query, CancellationToken ct = default)
    {
        var services = await db.MetricsSnapshots.AsNoTracking()
            .Select(s => s.ServiceName)
            .Distinct()
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(query.ServiceName))
            services = services.Where(s => s == query.ServiceName).ToList();

        var result = new List<MetricsSnapshot>();
        foreach (var service in services)
        {
            var latest = await db.MetricsSnapshots.AsNoTracking()
                .Where(s => s.ServiceName == service)
                .OrderByDescending(s => s.CapturedAt)
                .FirstOrDefaultAsync(ct);
            if (latest is not null)
                result.Add(latest);
        }

        return result
            .OrderBy(s => s.ServiceName)
            .Select(PerformanceMapper.ToSnapshotResponse)
            .ToList();
    }
}

/// <summary>指标历史查询（趋势图数据）</summary>
/// <param name="ServiceName">服务名（必填）</param>
/// <param name="From">开始时间（可选）</param>
/// <param name="To">结束时间（可选）</param>
/// <param name="Limit">返回条数上限（默认 500）</param>
public sealed record MetricsHistoryQuery(string ServiceName, DateTime? From = null, DateTime? To = null, int Limit = 500)
    : IQuery<List<MetricsSnapshotResponse>>;

/// <summary>指标历史查询处理器</summary>
public sealed class MetricsHistoryQueryHandler(PerformanceDbContext db)
    : IQueryHandler<MetricsHistoryQuery, List<MetricsSnapshotResponse>>
{
    /// <inheritdoc />
    public async Task<List<MetricsSnapshotResponse>> HandleAsync(MetricsHistoryQuery query, CancellationToken ct = default)
    {
        var limit = Math.Clamp(query.Limit, 1, 2000);
        var baseQuery = db.MetricsSnapshots.AsNoTracking().Where(s => s.ServiceName == query.ServiceName);
        if (query.From is not null)
            baseQuery = baseQuery.Where(s => s.CapturedAt >= query.From.Value.ToUniversalTime());
        if (query.To is not null)
            baseQuery = baseQuery.Where(s => s.CapturedAt <= query.To.Value.ToUniversalTime());

        var items = await baseQuery
            .OrderByDescending(s => s.CapturedAt)
            .Take(limit)
            .ToListAsync(ct);

        return items.OrderBy(s => s.CapturedAt).Select(PerformanceMapper.ToSnapshotResponse).ToList();
    }
}

/// <summary>已监控服务列表查询</summary>
public sealed record MonitoredServicesQuery() : IQuery<List<string>>;

/// <summary>已监控服务列表查询处理器（含本服务自身）</summary>
public sealed class MonitoredServicesQueryHandler(PerformanceDbContext db)
    : IQueryHandler<MonitoredServicesQuery, List<string>>
{
    /// <inheritdoc />
    public async Task<List<string>> HandleAsync(MonitoredServicesQuery query, CancellationToken ct = default)
    {
        return await db.MetricsSnapshots.AsNoTracking()
            .Select(s => s.ServiceName)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(ct);
    }
}

/// <summary>告警列表查询</summary>
/// <param name="Status">状态过滤（Open/Resolved，可选）</param>
/// <param name="ServiceName">服务名过滤（可选）</param>
/// <param name="Page">页码（默认 1）</param>
/// <param name="PageSize">每页条数（默认 20，上限 100）</param>
public sealed record AlertListQuery(string? Status = null, string? ServiceName = null, int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<AlertResponse>>;

/// <summary>告警列表查询处理器</summary>
public sealed class AlertListQueryHandler(PerformanceDbContext db)
    : IQueryHandler<AlertListQuery, PagedResult<AlertResponse>>
{
    /// <inheritdoc />
    public async Task<PagedResult<AlertResponse>> HandleAsync(AlertListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var baseQuery = db.AlertRecords.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<AlertStatus>(query.Status, ignoreCase: true, out var status))
            baseQuery = baseQuery.Where(a => a.Status == status);
        if (!string.IsNullOrWhiteSpace(query.ServiceName))
            baseQuery = baseQuery.Where(a => a.ServiceName == query.ServiceName);

        var total = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<AlertResponse>(items.Select(PerformanceMapper.ToAlertResponse).ToList(), total, page, pageSize);
    }
}

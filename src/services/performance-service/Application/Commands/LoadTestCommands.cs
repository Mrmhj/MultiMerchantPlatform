using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using PerformanceService.Application.Services;
using PerformanceService.Domain.Entities;
using PerformanceService.DTOs;
using PerformanceService.Infrastructure.Persistence;

namespace PerformanceService.Application.Commands;

/// <summary>创建压测任务命令</summary>
/// <param name="Name">任务名称</param>
/// <param name="TargetUrl">目标 URL</param>
/// <param name="HttpMethod">HTTP 方法</param>
/// <param name="Concurrency">并发数</param>
/// <param name="DurationSeconds">持续时间（秒）</param>
/// <param name="BodyJson">请求体 JSON（可选）</param>
/// <param name="HeadersJson">请求头 JSON（可选）</param>
public sealed record CreateLoadTestTaskCommand(
    string Name, string TargetUrl, string HttpMethod, int Concurrency, int DurationSeconds,
    string? BodyJson = null, string? HeadersJson = null) : ICommand<LoadTestTaskResponse>;

/// <summary>创建压测任务命令处理器</summary>
public sealed class CreateLoadTestTaskCommandHandler(PerformanceDbContext db)
    : ICommandHandler<CreateLoadTestTaskCommand, LoadTestTaskResponse>
{
    /// <inheritdoc />
    public async Task<LoadTestTaskResponse> HandleAsync(CreateLoadTestTaskCommand command, CancellationToken ct = default)
    {
        var task = new LoadTestTask(command.Name, command.TargetUrl, command.HttpMethod,
            command.Concurrency, command.DurationSeconds, command.BodyJson, command.HeadersJson);
        db.LoadTestTasks.Add(task);
        await db.SaveChangesAsync(ct);
        return PerformanceMapper.ToTaskResponse(task);
    }
}

/// <summary>更新压测任务命令</summary>
/// <param name="Id">任务 ID</param>
/// <param name="Name">任务名称</param>
/// <param name="TargetUrl">目标 URL</param>
/// <param name="HttpMethod">HTTP 方法</param>
/// <param name="Concurrency">并发数</param>
/// <param name="DurationSeconds">持续时间（秒）</param>
/// <param name="BodyJson">请求体 JSON（可选）</param>
/// <param name="HeadersJson">请求头 JSON（可选）</param>
public sealed record UpdateLoadTestTaskCommand(
    Guid Id, string Name, string TargetUrl, string HttpMethod, int Concurrency, int DurationSeconds,
    string? BodyJson = null, string? HeadersJson = null) : ICommand<LoadTestTaskResponse>;

/// <summary>更新压测任务命令处理器</summary>
public sealed class UpdateLoadTestTaskCommandHandler(PerformanceDbContext db)
    : ICommandHandler<UpdateLoadTestTaskCommand, LoadTestTaskResponse>
{
    /// <inheritdoc />
    public async Task<LoadTestTaskResponse> HandleAsync(UpdateLoadTestTaskCommand command, CancellationToken ct = default)
    {
        var task = await db.LoadTestTasks.FirstOrDefaultAsync(t => t.Id == command.Id, ct)
            ?? throw new NotFoundException("压测任务", command.Id);

        task.Update(command.Name, command.TargetUrl, command.HttpMethod,
            command.Concurrency, command.DurationSeconds, command.BodyJson, command.HeadersJson);
        await db.SaveChangesAsync(ct);
        return PerformanceMapper.ToTaskResponse(task);
    }
}

/// <summary>删除压测任务命令</summary>
/// <param name="Id">任务 ID</param>
public sealed record DeleteLoadTestTaskCommand(Guid Id) : ICommand;

/// <summary>删除压测任务命令处理器</summary>
public sealed class DeleteLoadTestTaskCommandHandler(PerformanceDbContext db)
    : ICommandHandler<DeleteLoadTestTaskCommand>
{
    /// <inheritdoc />
    public async Task<Unit> HandleAsync(DeleteLoadTestTaskCommand command, CancellationToken ct = default)
    {
        var task = await db.LoadTestTasks.FirstOrDefaultAsync(t => t.Id == command.Id, ct)
            ?? throw new NotFoundException("压测任务", command.Id);

        db.LoadTestTasks.Remove(task);
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}

/// <summary>启用 / 停用压测任务命令</summary>
/// <param name="Id">任务 ID</param>
/// <param name="Enabled">是否启用</param>
public sealed record SetLoadTestTaskEnabledCommand(Guid Id, bool Enabled) : ICommand<LoadTestTaskResponse>;

/// <summary>启用 / 停用压测任务命令处理器（停用后不可启动压测）</summary>
public sealed class SetLoadTestTaskEnabledCommandHandler(PerformanceDbContext db)
    : ICommandHandler<SetLoadTestTaskEnabledCommand, LoadTestTaskResponse>
{
    /// <inheritdoc />
    public async Task<LoadTestTaskResponse> HandleAsync(SetLoadTestTaskEnabledCommand command, CancellationToken ct = default)
    {
        var task = await db.LoadTestTasks.FirstOrDefaultAsync(t => t.Id == command.Id, ct)
            ?? throw new NotFoundException("压测任务", command.Id);

        if (command.Enabled) task.Enable();
        else task.Disable();

        await db.SaveChangesAsync(ct);
        return PerformanceMapper.ToTaskResponse(task);
    }
}

/// <summary>启动压测命令（创建运行批次并入队）</summary>
/// <param name="TaskId">任务 ID</param>
public sealed record RunLoadTestCommand(Guid TaskId) : ICommand<LoadTestRunResponse>;

/// <summary>启动压测命令处理器：校验任务启用 → 创建 Queued 批次 → 加入引擎队列</summary>
public sealed class RunLoadTestCommandHandler(
    PerformanceDbContext db,
    LoadTestEngine engine,
    TimeProvider timeProvider) : ICommandHandler<RunLoadTestCommand, LoadTestRunResponse>
{
    /// <inheritdoc />
    public async Task<LoadTestRunResponse> HandleAsync(RunLoadTestCommand command, CancellationToken ct = default)
    {
        var task = await db.LoadTestTasks.FirstOrDefaultAsync(t => t.Id == command.TaskId, ct)
            ?? throw new NotFoundException("压测任务", command.TaskId);
        if (!task.Enabled)
            throw new DomainException("压测任务已停用，无法启动", "LOADTEST_TASK_DISABLED");

        var run = new LoadTestRun(task.Id, task.Name, task.TargetUrl, task.HttpMethod,
            task.Concurrency, task.DurationSeconds, task.BodyJson, task.HeadersJson,
            timeProvider.GetUtcNow().UtcDateTime);
        db.LoadTestRuns.Add(run);
        await db.SaveChangesAsync(ct);

        engine.Enqueue(run.Id);
        return PerformanceMapper.ToRunResponse(run);
    }
}

/// <summary>停止压测命令（取消执行中 / 排队中的运行批次）</summary>
/// <param name="RunId">运行批次 ID</param>
public sealed record StopLoadTestCommand(Guid RunId) : ICommand<LoadTestRunResponse>;

/// <summary>停止压测命令处理器</summary>
public sealed class StopLoadTestCommandHandler(
    PerformanceDbContext db,
    LoadTestEngine engine) : ICommandHandler<StopLoadTestCommand, LoadTestRunResponse>
{
    /// <inheritdoc />
    public async Task<LoadTestRunResponse> HandleAsync(StopLoadTestCommand command, CancellationToken ct = default)
    {
        var run = await db.LoadTestRuns.FirstOrDefaultAsync(r => r.Id == command.RunId, ct)
            ?? throw new NotFoundException("压测运行批次", command.RunId);
        if (!run.CanCancel)
            throw new DomainException($"当前状态（{run.Status}）不允许停止", "LOADTEST_RUN_NOT_STOPPABLE");

        await engine.StopAsync(run.Id);
        return PerformanceMapper.ToRunResponse(run);
    }
}

/// <summary>手动关闭告警命令</summary>
/// <param name="AlertId">告警 ID</param>
public sealed record ResolveAlertCommand(Guid AlertId) : ICommand<AlertResponse>;

/// <summary>手动关闭告警命令处理器</summary>
public sealed class ResolveAlertCommandHandler(
    PerformanceDbContext db,
    TimeProvider timeProvider) : ICommandHandler<ResolveAlertCommand, AlertResponse>
{
    /// <inheritdoc />
    public async Task<AlertResponse> HandleAsync(ResolveAlertCommand command, CancellationToken ct = default)
    {
        var alert = await db.AlertRecords.FirstOrDefaultAsync(a => a.Id == command.AlertId, ct)
            ?? throw new NotFoundException("告警记录", command.AlertId);

        alert.Resolve(timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return PerformanceMapper.ToAlertResponse(alert);
    }
}

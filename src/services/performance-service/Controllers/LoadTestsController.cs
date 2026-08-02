using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PerformanceService.Application.Commands;
using PerformanceService.Application.Queries;
using PerformanceService.DTOs;
using PerformanceService.Infrastructure;

namespace PerformanceService.Controllers;

/// <summary>
/// 压测管理接口（平台端）— 压测任务 CRUD / 启动 / 停止 / 运行历史 / 报告下载，需 admin 角色。
/// </summary>
[ApiController]
[Authorize(Roles = "admin")]
[Route("api/performance/load-tests")]
[Produces("application/json")]
public sealed class LoadTestsController(
    IMediator mediator,
    IOptions<ReportOptions> reportOptions) : ControllerBase
{
    /// <summary>创建压测任务</summary>
    /// <param name="request">压测任务配置</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 创建的任务；400 — 参数非法</returns>
    /// <response code="200">创建成功</response>
    /// <response code="400">参数非法（URL/并发/时长等）</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoadTestTaskResponse>> Create(
        [FromBody] CreateLoadTestTaskRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<CreateLoadTestTaskCommand, LoadTestTaskResponse>(
                new CreateLoadTestTaskCommand(request.Name, request.TargetUrl, request.HttpMethod,
                    request.Concurrency, request.DurationSeconds, request.BodyJson, request.HeadersJson), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>更新压测任务</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="request">压测任务配置</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的任务；400 — 参数非法；404 — 任务不存在</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">参数非法</response>
    /// <response code="404">任务不存在</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoadTestTaskResponse>> Update(
        Guid id, [FromBody] UpdateLoadTestTaskRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<UpdateLoadTestTaskCommand, LoadTestTaskResponse>(
                new UpdateLoadTestTaskCommand(id, request.Name, request.TargetUrl, request.HttpMethod,
                    request.Concurrency, request.DurationSeconds, request.BodyJson, request.HeadersJson), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>删除压测任务</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>204 — 删除成功；404 — 任务不存在</returns>
    /// <response code="204">删除成功</response>
    /// <response code="404">任务不存在</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await mediator.SendAsync<DeleteLoadTestTaskCommand, Unit>(new DeleteLoadTestTaskCommand(id), ct);
            return NoContent();
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>启用 / 停用压测任务</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="enabled">是否启用</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的任务；404 — 任务不存在</returns>
    /// <response code="200">更新成功</response>
    /// <response code="404">任务不存在</response>
    [HttpPut("{id:guid}/enabled")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoadTestTaskResponse>> SetEnabled(
        Guid id, [FromQuery] bool enabled, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<SetLoadTestTaskEnabledCommand, LoadTestTaskResponse>(
                new SetLoadTestTaskEnabledCommand(id, enabled), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>启动压测（创建运行批次并后台执行）</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 运行批次（Queued）；400 — 任务停用；404 — 任务不存在</returns>
    /// <response code="200">已入队</response>
    /// <response code="400">任务停用</response>
    /// <response code="404">任务不存在</response>
    [HttpPost("{id:guid}/run")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoadTestRunResponse>> Run(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<RunLoadTestCommand, LoadTestRunResponse>(
                new RunLoadTestCommand(id), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>停止压测运行</summary>
    /// <param name="runId">运行批次 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 已请求停止；400 — 当前状态不可停止；404 — 批次不存在</returns>
    /// <response code="200">已请求停止</response>
    /// <response code="400">状态不可停止</response>
    /// <response code="404">批次不存在</response>
    [HttpPost("runs/{runId:guid}/stop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoadTestRunResponse>> Stop(Guid runId, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<StopLoadTestCommand, LoadTestRunResponse>(
                new StopLoadTestCommand(runId), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>压测任务列表（分页）</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 任务分页列表</returns>
    /// <response code="200">任务列表</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<LoadTestTaskResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await mediator.QueryAsync<LoadTestTaskListQuery, PagedResult<LoadTestTaskResponse>>(
            new LoadTestTaskListQuery(page, pageSize), ct));
    }

    /// <summary>压测运行历史（分页，可按任务 / 状态过滤）</summary>
    /// <param name="taskId">任务 ID（可选）</param>
    /// <param name="status">状态（Queued/Running/Completed/Failed/Cancelled，可选）</param>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 运行批次分页列表</returns>
    /// <response code="200">运行历史</response>
    [HttpGet("runs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<LoadTestRunResponse>>> Runs(
        [FromQuery] Guid? taskId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await mediator.QueryAsync<LoadTestRunListQuery, PagedResult<LoadTestRunResponse>>(
            new LoadTestRunListQuery(taskId, status, page, pageSize), ct));
    }

    /// <summary>压测运行详情</summary>
    /// <param name="runId">运行批次 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 运行详情；404 — 批次不存在</returns>
    /// <response code="200">运行详情</response>
    /// <response code="404">批次不存在</response>
    [HttpGet("runs/{runId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoadTestRunResponse>> RunDetail(Guid runId, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.QueryAsync<LoadTestRunDetailQuery, LoadTestRunResponse>(
                new LoadTestRunDetailQuery(runId), ct));
        }
        catch (DomainException ex)
        {
            return NotFound(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>下载压测报告（HTML）</summary>
    /// <param name="runId">运行批次 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — HTML 报告；404 — 批次不存在或未生成报告</returns>
    /// <response code="200">HTML 报告</response>
    /// <response code="404">报告不存在</response>
    [HttpGet("runs/{runId:guid}/report")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DownloadReport(Guid runId, CancellationToken ct)
    {
        LoadTestRunResponse run;
        try
        {
            run = await mediator.QueryAsync<LoadTestRunDetailQuery, LoadTestRunResponse>(
                new LoadTestRunDetailQuery(runId), ct);
        }
        catch (DomainException ex)
        {
            return NotFound(new { error = ex.Message, code = ex.ErrorCode });
        }

        if (string.IsNullOrWhiteSpace(run.ReportPath))
            return NotFound(new { error = "该运行批次未生成报告（未完成或被取消）", code = "REPORT_NOT_FOUND" });

        var fullPath = Path.Combine(reportOptions.Value.Directory, run.ReportPath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { error = "报告文件不存在", code = "REPORT_NOT_FOUND" });

        return PhysicalFile(fullPath, "text/html; charset=utf-8", Path.GetFileName(fullPath));
    }
}

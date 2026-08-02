using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using NotificationService.Application.Commands;
using NotificationService.Application.Queries;
using NotificationService.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NotificationService.Controllers;

/// <summary>
/// 通知模板管理接口（平台端）— 模板 CRUD / 启停，需 admin 角色。
/// 模板供内部接口按 Code 一键渲染发送，各业务场景无需重复造内容。
/// </summary>
[ApiController]
[Authorize(Roles = "admin")]
[Route("api/notifications/templates")]
[Produces("application/json")]
public sealed class NotificationTemplatesController(IMediator mediator) : ControllerBase
{
    /// <summary>模板列表（可按启用状态过滤）</summary>
    /// <param name="activeOnly">仅启用模板（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 模板列表</returns>
    /// <response code="200">模板列表</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<NotificationTemplateResponse>>> List(
        [FromQuery] bool? activeOnly, CancellationToken ct)
        => Ok(await mediator.QueryAsync<NotificationTemplateListQuery, IReadOnlyList<NotificationTemplateResponse>>(
            new NotificationTemplateListQuery(activeOnly), ct));

    /// <summary>模板详情</summary>
    /// <param name="id">模板 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 模板详情；404 — 模板不存在</returns>
    /// <response code="200">模板详情</response>
    /// <response code="404">模板不存在</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationTemplateResponse>> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.QueryAsync<NotificationTemplateByIdQuery, NotificationTemplateResponse>(
                new NotificationTemplateByIdQuery(id), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "模板不存在" });
        }
    }

    /// <summary>创建模板</summary>
    /// <param name="request">模板配置</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 创建的模板；400 — 参数校验失败</returns>
    /// <response code="200">创建成功</response>
    /// <response code="400">参数校验失败</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NotificationTemplateResponse>> Create(
        [FromBody] SaveNotificationTemplateRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<CreateNotificationTemplateCommand, NotificationTemplateResponse>(
                new CreateNotificationTemplateCommand(request), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>更新模板</summary>
    /// <param name="id">模板 ID</param>
    /// <param name="request">模板配置</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的模板；400 — 参数校验失败；404 — 模板不存在</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">参数校验失败</response>
    /// <response code="404">模板不存在</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationTemplateResponse>> Update(
        Guid id, [FromBody] SaveNotificationTemplateRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<UpdateNotificationTemplateCommand, NotificationTemplateResponse>(
                new UpdateNotificationTemplateCommand(id, request), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "模板不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>启用/停用模板</summary>
    /// <param name="id">模板 ID</param>
    /// <param name="enabled">是否启用</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的模板；404 — 模板不存在</returns>
    /// <response code="200">操作成功</response>
    /// <response code="404">模板不存在</response>
    [HttpPost("{id:guid}/enabled")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationTemplateResponse>> SetEnabled(
        Guid id, [FromQuery] bool enabled = true, CancellationToken ct = default)
    {
        try
        {
            return Ok(await mediator.SendAsync<SetNotificationTemplateEnabledCommand, NotificationTemplateResponse>(
                new SetNotificationTemplateEnabledCommand(id, enabled), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "模板不存在" });
        }
    }

    /// <summary>删除模板（物理删除）</summary>
    /// <param name="id">模板 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>204 — 删除成功；404 — 模板不存在</returns>
    /// <response code="204">删除成功</response>
    /// <response code="404">模板不存在</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await mediator.SendAsync<DeleteNotificationTemplateCommand, Unit>(
                new DeleteNotificationTemplateCommand(id), ct);
            return NoContent();
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "模板不存在" });
        }
    }
}

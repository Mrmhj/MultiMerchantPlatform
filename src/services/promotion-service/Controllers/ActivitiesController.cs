using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PromotionService.Application.Commands;
using PromotionService.Application.Queries;
using PromotionService.DTOs;

namespace PromotionService.Controllers;

/// <summary>
/// 满减活动管理接口（商户端）— 创建/列表/详情/启停，需 X-Merchant-Id 请求头；
/// 另提供 C 端进行中活动查询（公开）。
/// </summary>
[ApiController]
[Authorize]
[Route("api/promotion/activities")]
[Produces("application/json")]
public sealed class ActivitiesController(IMediator mediator) : ControllerBase
{
    /// <summary>创建满减活动</summary>
    /// <param name="request">活动信息</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 创建成功（初始 Draft）；400 — 缺商户上下文或参数错误</returns>
    /// <response code="201">创建成功</response>
    /// <response code="400">缺商户上下文（X-Merchant-Id）或参数错误</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ActivityResponse>> Create([FromBody] CreateActivityRequest request, CancellationToken ct)
    {
        try
        {
            return Created("", await mediator.SendAsync<CreateActivityCommand, ActivityResponse>(
                new CreateActivityCommand(request), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>活动列表（当前商户，分页，支持状态过滤）</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="status">状态过滤：all（默认）/draft/active/ended</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 分页活动列表</returns>
    /// <response code="200">列表数据</response>
    /// <response code="400">缺商户上下文</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ActivityResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null, CancellationToken ct = default)
    {
        try
        {
            return Ok(await mediator.QueryAsync<ListActivitiesQuery, PagedResult<ActivityResponse>>(
                new ListActivitiesQuery(page, pageSize, status), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>活动详情</summary>
    /// <param name="id">活动 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 活动；404 — 不存在或不属于当前商户</returns>
    /// <response code="200">活动数据</response>
    /// <response code="404">不存在</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActivityResponse>> Get(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.QueryAsync<GetActivityQuery, ActivityResponse>(new GetActivityQuery(id), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "满减活动不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>变更活动状态（启用/停用）</summary>
    /// <param name="id">活动 ID</param>
    /// <param name="request">目标状态（active=true/false）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的活动</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">缺商户上下文或状态非法（已结束不可启用）</response>
    /// <response code="404">不存在</response>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActivityResponse>> ChangeStatus(
        Guid id, [FromBody] ChangeActivityStatusRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<ChangeActivityStatusCommand, ActivityResponse>(
                new ChangeActivityStatusCommand(id, request.Active), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "满减活动不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>进行中满减活动（C 端公开，仅 Active 且时间窗口内）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 进行中活动列表</returns>
    /// <response code="200">活动列表</response>
    [HttpGet("active")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ActivityResponse>>> Active(CancellationToken ct)
    {
        return Ok(await mediator.QueryAsync<ActiveActivitiesQuery, List<ActivityResponse>>(new ActiveActivitiesQuery(), ct));
    }
}

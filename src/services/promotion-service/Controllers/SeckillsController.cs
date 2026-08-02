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
/// 秒杀活动管理接口（商户端）— 创建/列表/详情/启停，需 X-Merchant-Id 请求头。
/// 秒杀流程：创建（Draft）→ 启用（Active，Redis 预热库存）→ 买家抢购（缓存预扣 + 异步下单）。
/// </summary>
[ApiController]
[Authorize]
[Route("api/promotion/seckills")]
[Produces("application/json")]
public sealed class SeckillsController(IMediator mediator) : ControllerBase
{
    /// <summary>创建秒杀活动</summary>
    /// <param name="request">秒杀活动信息</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 创建成功（初始 Draft）；400 — 缺商户上下文或参数错误</returns>
    /// <response code="201">创建成功</response>
    /// <response code="400">缺商户上下文（X-Merchant-Id）或参数错误</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SeckillResponse>> Create([FromBody] CreateSeckillRequest request, CancellationToken ct)
    {
        try
        {
            return Created("", await mediator.SendAsync<CreateSeckillCommand, SeckillResponse>(
                new CreateSeckillCommand(request), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>秒杀活动列表（当前商户，分页，支持状态过滤）</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="status">状态过滤：all（默认）/draft/active/ended</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 分页秒杀活动列表</returns>
    /// <response code="200">列表数据</response>
    /// <response code="400">缺商户上下文</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<SeckillResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null, CancellationToken ct = default)
    {
        try
        {
            return Ok(await mediator.QueryAsync<ListSeckillsQuery, PagedResult<SeckillResponse>>(
                new ListSeckillsQuery(page, pageSize, status), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>秒杀活动详情</summary>
    /// <param name="id">活动 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 活动；404 — 不存在或不属于当前商户</returns>
    /// <response code="200">活动数据</response>
    /// <response code="404">不存在</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeckillResponse>> Get(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.QueryAsync<GetSeckillQuery, SeckillResponse>(new GetSeckillQuery(id), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "秒杀活动不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>变更秒杀活动状态（启用/停用）— 启用时 Redis 预热库存</summary>
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
    public async Task<ActionResult<SeckillResponse>> ChangeStatus(
        Guid id, [FromBody] ChangeSeckillStatusRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<ChangeSeckillStatusCommand, SeckillResponse>(
                new ChangeSeckillStatusCommand(id, request.Active), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "秒杀活动不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>进行中秒杀活动（C 端公开，仅 Active 且时间窗口内）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 进行中秒杀活动列表</returns>
    /// <response code="200">活动列表</response>
    [HttpGet("active")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SeckillResponse>>> Active(CancellationToken ct)
    {
        return Ok(await mediator.QueryAsync<ActiveSeckillsQuery, List<SeckillResponse>>(new ActiveSeckillsQuery(), ct));
    }
}

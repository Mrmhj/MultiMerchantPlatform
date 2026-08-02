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
/// 优惠券管理接口（商户端）— 创建/列表/详情/启停，需 X-Merchant-Id 请求头。
/// </summary>
[ApiController]
[Authorize]
[Route("api/promotion/coupons")]
[Produces("application/json")]
public sealed class CouponsController(IMediator mediator) : ControllerBase
{
    /// <summary>创建优惠券（满减券）</summary>
    /// <param name="request">券信息</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 创建成功；400 — 缺商户上下文或参数错误</returns>
    /// <response code="201">创建成功</response>
    /// <response code="400">缺商户上下文（X-Merchant-Id）或参数错误</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CouponResponse>> Create([FromBody] CreateCouponRequest request, CancellationToken ct)
    {
        try
        {
            return Created("", await mediator.SendAsync<CreateCouponCommand, CouponResponse>(
                new CreateCouponCommand(request), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>优惠券列表（当前商户，分页）</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 分页优惠券列表</returns>
    /// <response code="200">列表数据</response>
    /// <response code="400">缺商户上下文</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<CouponResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            return Ok(await mediator.QueryAsync<ListCouponsQuery, PagedResult<CouponResponse>>(
                new ListCouponsQuery(page, pageSize), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>优惠券详情</summary>
    /// <param name="id">优惠券模板 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 优惠券；404 — 不存在或不属于当前商户</returns>
    /// <response code="200">优惠券数据</response>
    /// <response code="400">缺商户上下文</response>
    /// <response code="404">不存在</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CouponResponse>> Get(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.QueryAsync<GetCouponQuery, CouponResponse>(new GetCouponQuery(id), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "优惠券不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>变更优惠券状态（启用/停用）</summary>
    /// <param name="id">优惠券模板 ID</param>
    /// <param name="request">目标状态（active=true/false）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的优惠券</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">缺商户上下文或状态非法</response>
    /// <response code="404">不存在</response>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CouponResponse>> ChangeStatus(
        Guid id, [FromBody] ChangeCouponStatusRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<ChangeCouponStatusCommand, CouponResponse>(
                new ChangeCouponStatusCommand(id, request.Active), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "优惠券不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }
}

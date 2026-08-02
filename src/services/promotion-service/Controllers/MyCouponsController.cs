using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PromotionService.Application.Commands;
using PromotionService.Application.Queries;
using PromotionService.DTOs;

namespace PromotionService.Controllers;

/// <summary>
/// 买家优惠券接口（C 端）— 可领券列表（公开）、领券、我的券，JWT 鉴权。
/// </summary>
[ApiController]
[Route("api/promotion")]
[Produces("application/json")]
public sealed class MyCouponsController(IMediator mediator, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>可领取优惠券列表（公开，启用 + 有效期窗口内 + 未领完）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 可领优惠券列表</returns>
    /// <response code="200">优惠券列表</response>
    [HttpGet("coupons/available")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CouponResponse>>> Available(CancellationToken ct)
    {
        return Ok(await mediator.QueryAsync<AvailableCouponsQuery, List<CouponResponse>>(new AvailableCouponsQuery(), ct));
    }

    /// <summary>领取优惠券（登录后）</summary>
    /// <param name="id">优惠券模板 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 领取到的用户券；400 — 限领/不可领；404 — 券不存在</returns>
    /// <response code="200">领取成功</response>
    /// <response code="400">不可领取（未启用/未到有效期/已领完/达限领数）</response>
    /// <response code="401">未登录</response>
    /// <response code="404">券不存在</response>
    [HttpPost("coupons/{id:guid}/claim")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserCouponResponse>> Claim(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<ClaimCouponCommand, UserCouponResponse>(
                new ClaimCouponCommand(currentUser.UserId, id), ct));
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

    /// <summary>我的优惠券（登录后，status 过滤）</summary>
    /// <param name="status">过滤状态：all（默认）/unused/used/expired</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 我的优惠券列表</returns>
    /// <response code="200">优惠券列表</response>
    /// <response code="401">未登录</response>
    [HttpGet("my/coupons")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<UserCouponResponse>>> My(
        [FromQuery] string? status = null, CancellationToken ct = default)
    {
        return Ok(await mediator.QueryAsync<MyCouponsQuery, List<UserCouponResponse>>(
            new MyCouponsQuery(currentUser.UserId, status), ct));
    }
}

using BuildingBlocks.Core.CQRS;
using Microsoft.AspNetCore.Mvc;
using PromotionService.Application.Commands;
using PromotionService.DTOs;

namespace PromotionService.Controllers;

/// <summary>
/// 促销内部维护接口 — 仅供 order-service 通过 X-Internal-Key 调用，不对外暴露。
/// 当前提供优惠券核销（支付确认时调用）。
/// </summary>
[ApiController]
[Route("api/promotion/internal")]
[Produces("application/json")]
public sealed class InternalPromotionController(IMediator mediator, IConfiguration configuration) : ControllerBase
{
    private readonly string _internalKey = configuration["Internal:Key"] ?? string.Empty;

    /// <summary>核销用户优惠券（支付确认时调用，成功返回优惠金额）</summary>
    /// <param name="request">核销请求（UserId + UserCouponId + 订单号）</param>
    /// <param name="key">内部密钥（请求头 X-Internal-Key）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 核销结果（Success/Error/优惠金额）</returns>
    /// <response code="200">核销结果</response>
    /// <response code="401">内部密钥错误</response>
    [HttpPost("coupons/use")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UseCouponResult>> Use(
        [FromBody] UseUserCouponRequest request,
        [FromHeader(Name = "X-Internal-Key")] string key,
        CancellationToken ct)
    {
        if (key != _internalKey)
            return Unauthorized(new { error = "内部密钥无效" });

        var command = new UseUserCouponCommand(request.UserId, request.UserCouponId, request.OrderId);
        return Ok(await mediator.SendAsync<UseUserCouponCommand, UseCouponResult>(command, ct));
    }
}

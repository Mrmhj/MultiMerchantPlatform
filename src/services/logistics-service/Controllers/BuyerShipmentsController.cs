using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Security;
using LogisticsService.Application.Queries;
using LogisticsService.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogisticsService.Controllers;

/// <summary>
/// 买家物流接口（C 端）— 按子订单查询我的运单 + 轨迹，JWT 鉴权。
/// </summary>
[ApiController]
[Authorize]
[Route("api/logistics/shipments")]
[Produces("application/json")]
public sealed class BuyerShipmentsController(IMediator mediator, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>我的子订单物流（含轨迹）</summary>
    /// <param name="subOrderId">子订单 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 运单详情；404 — 无该子订单运单</returns>
    /// <response code="200">运单详情</response>
    /// <response code="401">未登录</response>
    /// <response code="404">无该子订单运单</response>
    [HttpGet("my/{subOrderId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShipmentResponse>> My(Guid subOrderId, CancellationToken ct = default)
    {
        var result = await mediator.QueryAsync<BuyerShipmentQuery, ShipmentResponse?>(
            new BuyerShipmentQuery(currentUser.UserId, subOrderId), ct);
        return result is null ? NotFound(new { error = "无该子订单的物流信息" }) : Ok(result);
    }
}

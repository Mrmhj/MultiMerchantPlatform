using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Security;
using CartService.Application.Commands;
using CartService.Application.Queries;
using CartService.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CartService.Controllers;

/// <summary>
/// 购物车接口 — 买家登录后管理自己的购物车（同 SKU 自动合并）。
/// </summary>
[ApiController]
[Route("api/cart")]
[Authorize]
public sealed class CartController(IMediator mediator, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>加入购物车（同 SKU 自动合并数量）</summary>
    /// <param name="request">商品条目信息</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>购物车条目</returns>
    /// <response code="200">加购成功</response>
    /// <response code="401">未登录</response>
    [HttpPost("items")]
    [ProducesResponseType(typeof(CartItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CartItemResponse>> Add(
        [FromBody] AddCartItemRequest request, CancellationToken ct)
    {
        var result = await mediator.SendAsync<AddCartItemCommand, CartItemResponse>(
            new AddCartItemCommand(currentUser.UserId, request), ct);
        return Ok(result);
    }

    /// <summary>我的购物车（含选中合计）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>购物车全量（条目 + 合计）</returns>
    /// <response code="200">购物车数据</response>
    [HttpGet]
    [ProducesResponseType(typeof(CartResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CartResponse>> GetMyCart(CancellationToken ct)
    {
        var result = await mediator.QueryAsync<GetMyCartQuery, CartResponse>(new GetMyCartQuery(currentUser.UserId), ct);
        return Ok(result);
    }

    /// <summary>修改条目数量</summary>
    /// <param name="id">条目 ID</param>
    /// <param name="request">新数量</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>更新后的条目</returns>
    /// <response code="200">更新成功</response>
    /// <response code="404">条目不存在或不属于当前用户</response>
    [HttpPut("items/{id:guid}/quantity")]
    [ProducesResponseType(typeof(CartItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CartItemResponse>> UpdateQuantity(
        Guid id, [FromBody] UpdateQuantityRequest request, CancellationToken ct)
    {
        var result = await mediator.SendAsync<UpdateCartItemQuantityCommand, CartItemResponse>(
            new UpdateCartItemQuantityCommand(currentUser.UserId, id, request.Quantity), ct);
        return Ok(result);
    }

    /// <summary>设置条目选中状态</summary>
    /// <param name="id">条目 ID</param>
    /// <param name="request">选中状态</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>更新后的条目</returns>
    /// <response code="200">更新成功</response>
    /// <response code="404">条目不存在或不属于当前用户</response>
    [HttpPut("items/{id:guid}/select")]
    [ProducesResponseType(typeof(CartItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CartItemResponse>> Select(
        Guid id, [FromBody] SelectRequest request, CancellationToken ct)
    {
        var result = await mediator.SendAsync<SelectCartItemCommand, CartItemResponse>(
            new SelectCartItemCommand(currentUser.UserId, id, request.IsSelected), ct);
        return Ok(result);
    }

    /// <summary>删除条目</summary>
    /// <param name="id">条目 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>204 无内容</returns>
    /// <response code="204">删除成功</response>
    /// <response code="404">条目不存在或不属于当前用户</response>
    [HttpDelete("items/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    {
        await mediator.SendAsync<RemoveCartItemCommand, Unit>(new RemoveCartItemCommand(currentUser.UserId, id), ct);
        return NoContent();
    }

    /// <summary>清空购物车（默认只清选中项）</summary>
    /// <param name="onlySelected">true 只清选中项 / false 全清</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>204 无内容</returns>
    /// <response code="204">清空成功</response>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Clear([FromQuery] bool onlySelected = true, CancellationToken ct = default)
    {
        await mediator.SendAsync<ClearCartCommand, Unit>(new ClearCartCommand(currentUser.UserId, onlySelected), ct);
        return NoContent();
    }
}

using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using OrderService.Application.Commands;
using OrderService.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace OrderService.Controllers;

/// <summary>
/// 内部接口（服务间调用）— 供 pay-service 支付成功回调，X-Internal-Key 校验，不走买家鉴权。
/// </summary>
[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public sealed class InternalOrdersController(IMediator mediator, IConfiguration configuration) : ControllerBase
{
    private readonly string _internalKey = configuration["Internal:Key"] ?? string.Empty;

    /// <summary>内部支付确认（pay-service 支付成功后回调，请求头 X-Internal-Key）</summary>
    /// <param name="id">订单 ID</param>
    /// <param name="key">内部密钥（请求头 X-Internal-Key）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 支付后的订单；400 — 状态不允许；401 — 内部密钥无效；404 — 订单不存在</returns>
    [HttpPost("{id:guid}/pay-internal")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> PayInternal(
        Guid id,
        [FromHeader(Name = "X-Internal-Key")] string key,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_internalKey) || key != _internalKey)
            return Unauthorized(new { error = "内部密钥无效" });

        try
        {
            var command = new MarkOrderPaidInternalCommand(id);
            return Ok(await mediator.SendAsync<MarkOrderPaidInternalCommand, OrderResponse>(command, ct));
        }
        catch (NotFoundException)
        {
            return NotFound("订单不存在");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

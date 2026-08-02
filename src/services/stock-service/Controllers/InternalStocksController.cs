using BuildingBlocks.Core.CQRS;
using Microsoft.AspNetCore.Mvc;
using StockService.Application.Commands;
using StockService.DTOs;

namespace StockService.Controllers;

/// <summary>
/// 内部库存 API（服务间调用）— 预占/扣减/释放，X-Internal-Key 校验，供 order-service 下单/取消回调。
/// </summary>
[ApiController]
[Route("api/stocks/internal")]
[Produces("application/json")]
public sealed class InternalStocksController(IMediator mediator, IConfiguration configuration) : ControllerBase
{
    private readonly string _internalKey = configuration["Internal:Key"] ?? string.Empty;

    /// <summary>内部预占库存（下单时调用）</summary>
    /// <param name="request">内部库存请求（SkuId + 数量 + 关联订单号）</param>
    /// <param name="key">内部密钥（请求头 X-Internal-Key）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 操作结果（Success=false 表示库存不足）</returns>
    [HttpPost("reserve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<StockOperationResult>> Reserve(
        [FromBody] InternalStockRequest request,
        [FromHeader(Name = "X-Internal-Key")] string key,
        CancellationToken ct)
    {
        if (key != _internalKey)
            return Unauthorized(new { error = "内部密钥无效" });

        var command = new InternalReserveCommand(request.SkuId, request.Quantity, request.ReferenceId);
        return Ok(await mediator.SendAsync<InternalReserveCommand, StockOperationResult>(command, ct));
    }

    /// <summary>内部确认扣减（支付成功时调用）</summary>
    /// <param name="request">内部库存请求</param>
    /// <param name="key">内部密钥（请求头 X-Internal-Key）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 操作结果</returns>
    [HttpPost("confirm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<StockOperationResult>> Confirm(
        [FromBody] InternalStockRequest request,
        [FromHeader(Name = "X-Internal-Key")] string key,
        CancellationToken ct)
    {
        if (key != _internalKey)
            return Unauthorized(new { error = "内部密钥无效" });

        var command = new InternalConfirmCommand(request.SkuId, request.Quantity, request.ReferenceId);
        return Ok(await mediator.SendAsync<InternalConfirmCommand, StockOperationResult>(command, ct));
    }

    /// <summary>内部释放预占（订单取消时调用，回滚库存）</summary>
    /// <param name="request">内部库存请求</param>
    /// <param name="key">内部密钥（请求头 X-Internal-Key）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 操作结果</returns>
    [HttpPost("release")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<StockOperationResult>> Release(
        [FromBody] InternalStockRequest request,
        [FromHeader(Name = "X-Internal-Key")] string key,
        CancellationToken ct)
    {
        if (key != _internalKey)
            return Unauthorized(new { error = "内部密钥无效" });

        var command = new InternalReleaseCommand(request.SkuId, request.Quantity, request.ReferenceId);
        return Ok(await mediator.SendAsync<InternalReleaseCommand, StockOperationResult>(command, ct));
    }
}

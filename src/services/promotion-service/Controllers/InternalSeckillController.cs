using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Results;
using Microsoft.AspNetCore.Mvc;
using PromotionService.Application.Commands;
using PromotionService.DTOs;

namespace PromotionService.Controllers;

/// <summary>
/// 秒杀内部接口（服务间调用）— 供 order-service 异步下单成功后回调，X-Internal-Key 校验。
/// </summary>
[ApiController]
[Route("api/promotion/seckills/internal")]
[Produces("application/json")]
public sealed class InternalSeckillController(
    IMediator mediator,
    IConfiguration configuration) : ControllerBase
{
    private readonly string _internalKey = configuration["Internal:Key"] ?? string.Empty;

    /// <summary>秒杀记录标记订单已创建（order-service 异步下单成功后回调）</summary>
    /// <param name="recordId">秒杀记录 ID</param>
    /// <param name="request">订单信息（订单 ID + 订单号）</param>
    /// <param name="key">内部密钥（请求头 X-Internal-Key）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的秒杀记录；401 — 内部密钥无效；404 — 记录不存在</returns>
    /// <response code="200">更新成功（幂等：已 Ordered 重复回调返回当前状态）</response>
    /// <response code="401">内部密钥无效</response>
    /// <response code="404">记录不存在</response>
    [HttpPut("{recordId:guid}/order")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeckillRecordResponse>> MarkOrdered(
        Guid recordId,
        [FromBody] MarkSeckillOrderedRequest request,
        [FromHeader(Name = "X-Internal-Key")] string key,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_internalKey) || key != _internalKey)
            return Unauthorized(new { error = "内部密钥无效" });

        var result = await mediator.SendAsync<MarkSeckillOrderedCommand, Result<SeckillRecordResponse>>(
            new MarkSeckillOrderedCommand(recordId, request.OrderId, request.OrderNo), ct);

        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }
}

/// <summary>秒杀记录标记订单已创建请求（内部接口）</summary>
public sealed record MarkSeckillOrderedRequest
{
    /// <summary>订单 ID</summary>
    public Guid OrderId { get; init; }

    /// <summary>订单号</summary>
    public string OrderNo { get; init; } = string.Empty;
}

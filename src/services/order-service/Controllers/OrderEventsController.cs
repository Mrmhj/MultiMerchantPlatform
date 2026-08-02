using System.Text.Json;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Results;
using BuildingBlocks.Messaging;
using OrderService.Application.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OrderService.Controllers;

/// <summary>
/// 消息消费端点 — 供 messaging-service 投递集成事件回调（异步秒杀下单）。
/// 事件契约：EventName="SeckillOrderRequested"，Payload 为 promotion-service 发布的事件 JSON。
/// 幂等：消费端按秒杀记录 ID 去重（SeckillOrderProcessed 表），消息总线侧亦按订阅者去重。
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/orders")]
[Produces("application/json")]
public sealed class OrderEventsController(IMediator mediator) : ControllerBase
{
    private const string SeckillEventName = "SeckillOrderRequestedEvent";
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>接收集成事件（messaging-service 回调）</summary>
    /// <param name="envelope">消息信封（EventName + Payload）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 消费成功；400 — 事件类型不识别；500 — 业务处理失败（消息总线将重试）</returns>
    /// <response code="200">消费成功</response>
    /// <response code="400">事件类型不识别</response>
    /// <response code="500">业务处理失败（触发消息总线重试）</response>
    [HttpPost("events")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Consume([FromBody] MessageEnvelope envelope, CancellationToken ct)
    {
        if (!string.Equals(envelope.EventName, SeckillEventName, StringComparison.Ordinal))
            return BadRequest(new { error = $"不支持的事件类型: {envelope.EventName}" });

        var payload = JsonSerializer.Deserialize<SeckillOrderPayload>(envelope.Payload, PayloadOptions);
        if (payload is null)
            return BadRequest(new { error = "秒杀下单消息反序列化失败" });

        var command = new CreateSeckillOrderCommand(
            payload.RecordId, payload.UserId,
            payload.MerchantId, payload.MerchantName,
            payload.ProductId, payload.ProductName,
            payload.SkuId, payload.SkuCode, payload.Spec,
            payload.UnitPrice, payload.Quantity);

        var result = await mediator.SendAsync<CreateSeckillOrderCommand, Result<OrderService.DTOs.OrderResponse>>(command, ct);
        if (!result.IsSuccess)
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = result.Error });

        return Ok(new { orderId = result.Value.Id, orderNo = result.Value.OrderNo });
    }

    /// <summary>秒杀下单消息负载（与 promotion-service 的 SeckillOrderRequestedEvent 字段对齐）</summary>
    private sealed record SeckillOrderPayload
    {
        public Guid RecordId { get; init; }
        public Guid ActivityId { get; init; }
        public Guid MerchantId { get; init; }
        public string MerchantName { get; init; } = string.Empty;
        public Guid UserId { get; init; }
        public Guid ProductId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public Guid SkuId { get; init; }
        public string SkuCode { get; init; } = string.Empty;
        public string Spec { get; init; } = string.Empty;
        public decimal UnitPrice { get; init; }
        public int Quantity { get; init; }
        public DateTime ExpireAt { get; init; }
    }
}

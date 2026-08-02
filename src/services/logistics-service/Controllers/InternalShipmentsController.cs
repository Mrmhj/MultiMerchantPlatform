using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using LogisticsService.Application.Commands;
using LogisticsService.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogisticsService.Controllers;

/// <summary>
/// 内部接口（服务间调用）— order-service 发货后创建运单、物流轨迹推进模拟，X-Internal-Key 校验。
/// </summary>
[ApiController]
[Route("api/logistics")]
[Produces("application/json")]
public sealed class InternalShipmentsController(IMediator mediator, IConfiguration configuration) : ControllerBase
{
    private readonly string _internalKey = configuration["Internal:Key"] ?? string.Empty;

    /// <summary>内部创建运单（order-service 发货后回调）</summary>
    /// <param name="request">运单信息</param>
    /// <param name="key">内部密钥（请求头 X-Internal-Key）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 运单；400 — 子订单已存在运单或运单号重复；401 — 内部密钥无效</returns>
    /// <response code="201">创建成功</response>
    /// <response code="400">参数错误或运单重复</response>
    /// <response code="401">内部密钥无效</response>
    [HttpPost("internal/shipments")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ShipmentResponse>> Create(
        [FromBody] CreateShipmentInternalRequest request,
        [FromHeader(Name = "X-Internal-Key")] string key,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_internalKey) || key != _internalKey)
            return Unauthorized(new { error = "内部密钥无效" });

        try
        {
            return Created("", await mediator.SendAsync<CreateShipmentCommand, ShipmentResponse>(
                new CreateShipmentCommand(request), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>内部推进物流轨迹（模拟物流公司回调，演示用）</summary>
    /// <param name="request">推进信息（运单号 + 描述，可选标记异常）</param>
    /// <param name="key">内部密钥（请求头 X-Internal-Key）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 推进后的运单；400 — 状态不允许；401 — 内部密钥无效；404 — 运单不存在</returns>
    /// <response code="200">推进成功</response>
    /// <response code="400">状态不允许</response>
    /// <response code="401">内部密钥无效</response>
    /// <response code="404">运单不存在</response>
    [HttpPost("internal/tracks/advance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShipmentResponse>> Advance(
        [FromBody] AdvanceTrackInternalRequest request,
        [FromHeader(Name = "X-Internal-Key")] string key,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_internalKey) || key != _internalKey)
            return Unauthorized(new { error = "内部密钥无效" });

        try
        {
            return Ok(await mediator.SendAsync<AdvanceTrackCommand, ShipmentResponse>(
                new AdvanceTrackCommand(request.TrackingNo, request.Description, request.Location, request.MarkException), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "运单不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }
}

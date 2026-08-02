using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using BuildingBlocks.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PromotionService.Application.Commands;
using PromotionService.Application.Queries;
using PromotionService.DTOs;

namespace PromotionService.Controllers;

/// <summary>
/// 秒杀接口（C 端）— 抢购（缓存预扣 + 异步下单）、我的秒杀记录，JWT 鉴权。
/// 抢购成功后秒杀记录为 Pending，order-service 消费消息异步创建订单后回调标记 Ordered。
/// </summary>
[ApiController]
[Route("api/promotion")]
[Produces("application/json")]
public sealed class SeckillController(IMediator mediator, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>秒杀抢购（登录后）— Redis 原子预扣库存，防超卖；成功后异步下单</summary>
    /// <param name="id">秒杀活动 ID</param>
    /// <param name="request">购买数量</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 抢购结果（Success=true 表示预扣成功，等待异步下单）；400 — 未开始/已结束/超限购/售罄</returns>
    /// <response code="200">抢购结果</response>
    /// <response code="400">参数错误</response>
    /// <response code="401">未登录</response>
    /// <response code="404">活动不存在</response>
    [HttpPost("seckills/{id:guid}/buy")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BuySeckillResult>> Buy(
        Guid id, [FromBody] BuySeckillRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<BuySeckillCommand, BuySeckillResult>(
                new BuySeckillCommand(currentUser.UserId, id, request.Quantity), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "秒杀活动不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>我的秒杀记录（登录后，分页）</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 我的秒杀记录列表</returns>
    /// <response code="200">列表数据</response>
    /// <response code="401">未登录</response>
    [HttpGet("my/seckills")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<SeckillRecordResponse>>> My(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await mediator.QueryAsync<MySeckillRecordsQuery, PagedResult<SeckillRecordResponse>>(
            new MySeckillRecordsQuery(currentUser.UserId, page, pageSize), ct));
    }

    /// <summary>秒杀记录详情（登录后）</summary>
    /// <param name="id">秒杀记录 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 记录；404 — 不存在或不属于当前用户</returns>
    /// <response code="200">记录数据</response>
    /// <response code="401">未登录</response>
    /// <response code="404">不存在</response>
    [HttpGet("my/seckills/{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeckillRecordResponse>> GetRecord(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.QueryAsync<GetSeckillRecordQuery, SeckillRecordResponse>(
                new GetSeckillRecordQuery(currentUser.UserId, id), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "秒杀记录不存在" });
        }
    }
}

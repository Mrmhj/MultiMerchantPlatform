using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using BuildingBlocks.MultiTenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReviewService.Application.Commands;
using ReviewService.Application.Queries;
using ReviewService.DTOs;

namespace ReviewService.Controllers;

/// <summary>
/// 商户评价管理接口（商户端）— 评价列表 / 回复 / 隐藏恢复，需 X-Merchant-Id 请求头。
/// </summary>
[ApiController]
[Authorize]
[Route("api/reviews")]
[Produces("application/json")]
public sealed class MerchantReviewsController(IMediator mediator, ITenantProvider tenantProvider) : ControllerBase
{
    /// <summary>当前商户 ID（缺商户上下文抛业务异常）</summary>
    private Guid MerchantId => tenantProvider.CurrentMerchantId
        ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

    /// <summary>商户评价列表（分页，可按商品/评分/状态过滤）</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="productId">商品 ID 过滤（可选）</param>
    /// <param name="rating">评分过滤 1-5（可选）</param>
    /// <param name="status">状态过滤：all（默认）/visible/hidden</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 评价分页列表</returns>
    /// <response code="200">评价列表</response>
    /// <response code="400">缺商户上下文</response>
    [HttpGet("merchant")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ReviewResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] Guid? productId = null, [FromQuery] int? rating = null,
        [FromQuery] string? status = null, CancellationToken ct = default)
    {
        try
        {
            return Ok(await mediator.QueryAsync<MerchantReviewsQuery, PagedResult<ReviewResponse>>(
                new MerchantReviewsQuery(MerchantId, page, pageSize, productId, rating, status), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>回复评价（可修改，重复回复覆盖）</summary>
    /// <param name="id">评价 ID</param>
    /// <param name="request">回复内容</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的评价</returns>
    /// <response code="200">回复成功</response>
    /// <response code="400">缺商户上下文或回复内容非法</response>
    /// <response code="404">评价不存在或不属于当前商户</response>
    [HttpPut("{id:guid}/reply")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewResponse>> Reply(
        Guid id, [FromBody] ReplyReviewRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<ReplyReviewCommand, ReviewResponse>(
                new ReplyReviewCommand(MerchantId, id, request.Reply), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "评价不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>变更评价状态（隐藏违规评价 / 恢复可见）</summary>
    /// <param name="id">评价 ID</param>
    /// <param name="request">目标状态（visible=true/false）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的评价</returns>
    /// <response code="200">更新成功</response>
    /// <response code="400">缺商户上下文或状态非法</response>
    /// <response code="404">评价不存在或不属于当前商户</response>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewResponse>> ChangeStatus(
        Guid id, [FromBody] ChangeReviewStatusRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<ChangeReviewStatusCommand, ReviewResponse>(
                new ChangeReviewStatusCommand(MerchantId, id, request.Visible), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "评价不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }
}

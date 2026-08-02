using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using BuildingBlocks.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReviewService.Application.Commands;
using ReviewService.Application.Queries;
using ReviewService.DTOs;

namespace ReviewService.Controllers;

/// <summary>
/// 买家评价接口（C 端）— 创建评价 / 我的评价，JWT 鉴权。
/// </summary>
[ApiController]
[Authorize]
[Route("api/reviews")]
[Produces("application/json")]
public sealed class ReviewsController(IMediator mediator, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>创建评价（同一订单商品仅可评价一次）</summary>
    /// <param name="request">评价信息</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 评价；400 — 参数错误或重复评价；401 — 未登录</returns>
    /// <response code="201">创建成功</response>
    /// <response code="400">参数错误或该订单商品已评价</response>
    /// <response code="401">未登录</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ReviewResponse>> Create([FromBody] CreateReviewRequest request, CancellationToken ct)
    {
        try
        {
            return Created("", await mediator.SendAsync<CreateReviewCommand, ReviewResponse>(
                new CreateReviewCommand(currentUser.UserId, request), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>我的评价（分页）</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 我的评价分页列表</returns>
    /// <response code="200">评价列表</response>
    /// <response code="401">未登录</response>
    [HttpGet("my")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<ReviewResponse>>> My(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await mediator.QueryAsync<MyReviewsQuery, PagedResult<ReviewResponse>>(
            new MyReviewsQuery(currentUser.UserId, page, pageSize), ct));
    }
}

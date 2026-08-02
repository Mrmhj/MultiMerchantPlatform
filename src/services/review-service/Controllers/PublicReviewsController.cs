using BuildingBlocks.Core.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReviewService.Application.Queries;
using ReviewService.DTOs;

namespace ReviewService.Controllers;

/// <summary>
/// 商品评价公开接口（C 端）— 商品详情页评价列表 + 评分统计，无需鉴权。
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/reviews")]
[Produces("application/json")]
public sealed class PublicReviewsController(IMediator mediator) : ControllerBase
{
    /// <summary>商品评价列表（仅可见评价，含平均分/分布/分页）</summary>
    /// <param name="productId">商品 ID</param>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="rating">评分过滤 1-5（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 评价列表 + 评分统计</returns>
    /// <response code="200">评价数据</response>
    [HttpGet("product/{productId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductReviewsResponse>> Product(
        Guid productId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] int? rating = null, CancellationToken ct = default)
    {
        return Ok(await mediator.QueryAsync<ProductReviewsQuery, ProductReviewsResponse>(
            new ProductReviewsQuery(productId, page, pageSize, rating), ct));
    }
}

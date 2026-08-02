using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Results;
using SearchService.Application.Queries;
using SearchService.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace SearchService.Controllers;

/// <summary>
/// 商品搜索接口（C 端公开，无鉴权）— 仅检索在售商品索引。
/// </summary>
[ApiController]
[Route("api/search")]
public sealed class SearchController(IMediator mediator) : ControllerBase
{
    /// <summary>商品搜索（关键词/分类/价格区间 + 分页）</summary>
    /// <param name="keyword">关键词（匹配名称/描述）</param>
    /// <param name="categoryId">分类 ID（可选）</param>
    /// <param name="minPrice">最低价（可选）</param>
    /// <param name="maxPrice">最高价（可选）</param>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，最大 100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>分页搜索结果</returns>
    /// <response code="200">搜索结果</response>
    [HttpGet("products")]
    [ProducesResponseType(typeof(PagedResult<SearchResultItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SearchResultItem>>> Search(
        [FromQuery] string? keyword = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.QueryAsync<SearchProductsQuery, PagedResult<SearchResultItem>>(
            new SearchProductsQuery(keyword, categoryId, minPrice, maxPrice, page, pageSize), ct);
        return Ok(result);
    }
}

using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.Queries;
using ProductService.DTOs;

namespace ProductService.Controllers;

/// <summary>
/// 公开商品 API（C 端商城）— 无鉴权，仅在售商品。
/// </summary>
[ApiController]
[Route("api/products/public")]
[Produces("application/json")]
public sealed class PublicProductsController(IMediator mediator) : ControllerBase
{
    /// <summary>公开商品列表（在售，分页）</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 分页在售商品列表（含 SKU）</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new ListPublicProductsQuery(page, pageSize);
        return Ok(await mediator.QueryAsync<ListPublicProductsQuery, PagedResult<ProductResponse>>(query, ct));
    }

    /// <summary>公开商品详情（在售）</summary>
    /// <param name="id">商品 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 商品详情；404 — 商品不存在或未上架</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.QueryAsync<GetPublicProductQuery, ProductResponse>(new GetPublicProductQuery(id), ct));
        }
        catch (NotFoundException)
        {
            return NotFound("商品不存在或未上架");
        }
    }
}

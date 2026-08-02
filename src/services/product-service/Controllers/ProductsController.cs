using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using ProductService.Application.Commands;
using ProductService.Application.Queries;
using ProductService.Domain.Enums;
using ProductService.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ProductService.Controllers;

/// <summary>
/// 商品 API — 商品 CRUD / SKU / 上下架（需登录 + X-Merchant-Id 请求头，多租户隔离）。
/// </summary>
[ApiController]
[Authorize]
[Route("api/products")]
[Produces("application/json")]
public sealed class ProductsController(IMediator mediator) : ControllerBase
{
    /// <summary>创建商品（含 SKU 列表，初始状态草稿）</summary>
    /// <param name="request">商品创建请求（名称 + 分类 + SKU ≥ 1）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 商品记录；400 — 缺商户上下文或分类无效</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductResponse>> Create([FromBody] CreateProductRequest request, CancellationToken ct)
    {
        try
        {
            var command = new CreateProductCommand(
                request.Name, request.CategoryId, request.Description, request.CoverImage, request.Skus);
            return Created("", await mediator.SendAsync<CreateProductCommand, ProductResponse>(command, ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>商品分页列表（当前商户，状态过滤）</summary>
    /// <param name="status">按状态过滤（可选：1草稿 2在售 3下架）</param>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 分页商品列表（含 SKU）</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductResponse>>> List(
        [FromQuery] ProductStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new ListProductsQuery(status, page, pageSize);
        return Ok(await mediator.QueryAsync<ListProductsQuery, PagedResult<ProductResponse>>(query, ct));
    }

    /// <summary>商品详情（含 SKU）</summary>
    /// <param name="id">商品 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 商品详情；404 — 商品不存在或不属于当前商户</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.QueryAsync<GetProductQuery, ProductResponse>(new GetProductQuery(id), ct));
        }
        catch (NotFoundException)
        {
            return NotFound("商品不存在");
        }
    }

    /// <summary>更新商品基本信息</summary>
    /// <param name="id">商品 ID</param>
    /// <param name="request">商品更新请求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的商品；400 — 分类无效；404 — 商品不存在</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> Update(Guid id, [FromBody] UpdateProductRequest request, CancellationToken ct)
    {
        try
        {
            var command = new UpdateProductCommand(id, request.Name, request.CategoryId, request.Description, request.CoverImage);
            return Ok(await mediator.SendAsync<UpdateProductCommand, ProductResponse>(command, ct));
        }
        catch (NotFoundException)
        {
            return NotFound("商品不存在");
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>商品上下架（上架要求至少一个启用 SKU）</summary>
    /// <param name="id">商品 ID</param>
    /// <param name="request">状态请求（2=上架 3=下架）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的商品；400 — 状态非法或无 SKU；404 — 商品不存在</returns>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> UpdateStatus(Guid id, [FromBody] UpdateProductStatusRequest request, CancellationToken ct)
    {
        try
        {
            var command = new UpdateProductStatusCommand(id, request.Status);
            return Ok(await mediator.SendAsync<UpdateProductStatusCommand, ProductResponse>(command, ct));
        }
        catch (NotFoundException)
        {
            return NotFound("商品不存在");
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

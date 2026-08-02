using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockService.Application.Commands;
using StockService.Application.Queries;
using StockService.DTOs;

namespace StockService.Controllers;

/// <summary>
/// 库存 API — 商户库存管理（创建/列表/详情/补货/流水，需 X-Merchant-Id）。
/// </summary>
[ApiController]
[Authorize]
[Route("api/stocks")]
[Produces("application/json")]
public sealed class StocksController(IMediator mediator) : ControllerBase
{
    /// <summary>创建库存（SKU 初始总库存）</summary>
    /// <param name="request">创建库存请求（SkuId + 总库存）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 库存；400 — 缺商户上下文；409 — SKU 已建库存</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockResponse>> Create([FromBody] CreateStockRequest request, CancellationToken ct)
    {
        try
        {
            var command = new CreateStockCommand(request.SkuId, request.Total);
            return Created("", await mediator.SendAsync<CreateStockCommand, StockResponse>(command, ct));
        }
        catch (DomainException ex) when (ex.ErrorCode == "SKU_EXISTS")
        {
            return Conflict(new { error = ex.Message, code = ex.ErrorCode });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>库存列表（当前商户，分页）</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 分页库存列表</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<StockResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            var query = new ListStocksQuery(page, pageSize);
            return Ok(await mediator.QueryAsync<ListStocksQuery, PagedResult<StockResponse>>(query, ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>库存详情（按 SKU）</summary>
    /// <param name="skuId">SKU ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 库存；404 — 不存在</returns>
    [HttpGet("{skuId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockResponse>> GetBySku(Guid skuId, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.QueryAsync<GetStockQuery, StockResponse>(new GetStockQuery(skuId), ct));
        }
        catch (NotFoundException)
        {
            return NotFound("库存不存在");
        }
    }

    /// <summary>补货入库</summary>
    /// <param name="skuId">SKU ID</param>
    /// <param name="request">补货数量</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 补货后库存；404 — 不存在</returns>
    [HttpPost("{skuId:guid}/increase")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockResponse>> Increase(Guid skuId, [FromBody] IncreaseStockRequest request, CancellationToken ct)
    {
        try
        {
            var command = new IncreaseStockCommand(skuId, request.Quantity);
            return Ok(await mediator.SendAsync<IncreaseStockCommand, StockResponse>(command, ct));
        }
        catch (NotFoundException)
        {
            return NotFound("库存不存在");
        }
    }

    /// <summary>库存流水（按 SKU，审计）</summary>
    /// <param name="skuId">SKU ID</param>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 分页流水</returns>
    [HttpGet("{skuId:guid}/transactions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<StockTransactionResponse>>> Transactions(
        Guid skuId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new ListTransactionsQuery(skuId, page, pageSize);
        return Ok(await mediator.QueryAsync<ListTransactionsQuery, PagedResult<StockTransactionResponse>>(query, ct));
    }
}

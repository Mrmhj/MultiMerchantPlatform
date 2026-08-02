using BiAdminService.Application.Queries;
using BiAdminService.Application.Services;
using BiAdminService.DTOs;
using BuildingBlocks.Core.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiAdminService.Controllers;

/// <summary>
/// BI 分析 API（平台端）— 总览指标 / 销售趋势 / 商户排行 / 商品排行 / 订单状态分布 / 手动同步，需 admin 角色。
/// </summary>
[ApiController]
[Authorize(Roles = "admin")]
[Route("api/bi")]
[Produces("application/json")]
public sealed class BiController(IMediator mediator, BiSyncService syncService) : ControllerBase
{
    /// <summary>核心指标总览（GMV / 订单 / 商户 / 商品 / 用户）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 核心指标</returns>
    /// <response code="200">核心指标</response>
    [HttpGet("overview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<BiOverviewResponse>> Overview(CancellationToken ct)
    {
        return Ok(await mediator.QueryAsync<BiOverviewQuery, BiOverviewResponse>(new BiOverviewQuery(), ct));
    }

    /// <summary>销售趋势（按天 GMV + 订单数折线）</summary>
    /// <param name="days">最近天数（默认 30，上限 90）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 按天销售序列（升序）</returns>
    /// <response code="200">按天销售序列</response>
    [HttpGet("sales-trend")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BiSalesTrendPoint>>> SalesTrend([FromQuery] int days = 30, CancellationToken ct = default)
    {
        return Ok(await mediator.QueryAsync<BiSalesTrendQuery, List<BiSalesTrendPoint>>(new BiSalesTrendQuery(days), ct));
    }

    /// <summary>商户销售排行（GMV 降序柱状）</summary>
    /// <param name="top">返回条数（默认 10，上限 50）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 商户排行列表</returns>
    /// <response code="200">商户排行列表</response>
    [HttpGet("merchant-rank")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BiMerchantRankResponse>>> MerchantRank([FromQuery] int top = 10, CancellationToken ct = default)
    {
        return Ok(await mediator.QueryAsync<BiMerchantRankQuery, List<BiMerchantRankResponse>>(new BiMerchantRankQuery(top), ct));
    }

    /// <summary>商品销售排行（销售额降序柱状）</summary>
    /// <param name="top">返回条数（默认 10，上限 50）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 商品排行列表</returns>
    /// <response code="200">商品排行列表</response>
    [HttpGet("product-rank")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BiProductRankResponse>>> ProductRank([FromQuery] int top = 10, CancellationToken ct = default)
    {
        return Ok(await mediator.QueryAsync<BiProductRankQuery, List<BiProductRankResponse>>(new BiProductRankQuery(top), ct));
    }

    /// <summary>订单状态分布（饼图）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 状态分布列表</returns>
    /// <response code="200">状态分布列表</response>
    [HttpGet("order-status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BiOrderStatusResponse>>> OrderStatus(CancellationToken ct)
    {
        return Ok(await mediator.QueryAsync<BiOrderStatusQuery, List<BiOrderStatusResponse>>(new BiOrderStatusQuery(), ct));
    }

    /// <summary>手动触发一次数据同步（重建聚合表，拉取各服务内部统计）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 同步结果；502 — 上游取数失败</returns>
    /// <response code="200">同步完成</response>
    /// <response code="502">上游服务取数失败</response>
    [HttpPost("sync")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<BiSyncResponse>> Sync(CancellationToken ct)
    {
        var result = await syncService.SyncAsync(ct);
        if (!result.Success)
            return StatusCode(StatusCodes.Status502BadGateway, new BiSyncResponse(
                false, result.Error, 0, 0, 0, 0, 0, 0, 0, 0m, 0, DateTime.UtcNow));

        return Ok(new BiSyncResponse(
            true, null,
            result.DailySales, result.MerchantRows, result.ProductRows, result.StatusRows,
            result.MerchantCount, result.ProductCount, result.UserCount,
            result.TotalGmv, result.TotalOrders, DateTime.UtcNow));
    }
}

using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using SettlementService.Application.Commands;
using SettlementService.Application.Queries;
using SettlementService.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SettlementService.Controllers;

/// <summary>
/// 平台结算管理接口（平台端）— 生成结算单 / 确认结算 / 打款 / 结算单列表，需 admin 角色。
/// </summary>
[ApiController]
[Authorize(Roles = "admin")]
[Route("api/settlements")]
[Produces("application/json")]
public sealed class AdminSettlementsController(IMediator mediator) : ControllerBase
{
    /// <summary>生成结算单（扫描已完成子订单，按商户聚合 + 佣金规则计算）</summary>
    /// <param name="request">周期（可选，默认全部未结算）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 生成的结算单列表</returns>
    /// <response code="200">生成成功</response>
    [HttpPost("generate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<GenerateSettlementResponse>> Generate(
        [FromBody] GenerateSettlementRequest request, CancellationToken ct)
    {
        return Ok(await mediator.SendAsync<GenerateSettlementsCommand, GenerateSettlementResponse>(
            new GenerateSettlementsCommand(request.CycleStart, request.CycleEnd), ct));
    }

    /// <summary>结算单列表（分页，可按状态/商户过滤）</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="status">状态过滤（可选：pending/settled/paid）</param>
    /// <param name="merchantId">商户 ID 过滤（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 结算单分页列表</returns>
    /// <response code="200">结算单列表</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SettlementResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null, [FromQuery] Guid? merchantId = null, CancellationToken ct = default)
    {
        return Ok(await mediator.QueryAsync<AdminSettlementsQuery, PagedResult<SettlementResponse>>(
            new AdminSettlementsQuery(page, pageSize, status, merchantId), ct));
    }

    /// <summary>确认结算（Pending → Settled）</summary>
    /// <param name="id">结算单 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的结算单；400 — 状态不允许或无明细；404 — 结算单不存在</returns>
    /// <response code="200">确认成功</response>
    /// <response code="400">状态不允许或无明细</response>
    /// <response code="404">结算单不存在</response>
    [HttpPost("{id:guid}/settle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SettlementResponse>> Settle(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<SettleSettlementCommand, SettlementResponse>(
                new SettleSettlementCommand(id), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "结算单不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>标记已打款（Settled → Paid）</summary>
    /// <param name="id">结算单 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的结算单；400 — 状态不允许；404 — 结算单不存在</returns>
    /// <response code="200">打款成功</response>
    /// <response code="400">状态不允许</response>
    /// <response code="404">结算单不存在</response>
    [HttpPost("{id:guid}/paid")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SettlementResponse>> MarkPaid(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<MarkPaidSettlementCommand, SettlementResponse>(
                new MarkPaidSettlementCommand(id), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "结算单不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }
}

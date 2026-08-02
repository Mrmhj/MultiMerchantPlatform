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
/// 佣金规则管理接口（平台端）— 设置/更新商户佣金比例、规则列表，需 admin 角色。
/// </summary>
[ApiController]
[Authorize(Roles = "admin")]
[Route("api/commission-rules")]
[Produces("application/json")]
public sealed class AdminCommissionRulesController(IMediator mediator) : ControllerBase
{
    /// <summary>设置/更新商户佣金比例（存在则更新，不存在则创建）</summary>
    /// <param name="merchantId">商户 ID</param>
    /// <param name="request">佣金比例（0-100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 佣金规则；400 — 比例非法</returns>
    /// <response code="200">设置成功</response>
    /// <response code="400">比例非法</response>
    [HttpPut("{merchantId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CommissionRuleResponse>> Upsert(
        Guid merchantId, [FromBody] SaveCommissionRuleRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<UpsertCommissionRuleCommand, CommissionRuleResponse>(
                new UpsertCommissionRuleCommand(merchantId, request.Rate), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>佣金规则列表（分页）</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 规则分页列表</returns>
    /// <response code="200">规则列表</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CommissionRuleResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await mediator.QueryAsync<AdminCommissionRulesQuery, PagedResult<CommissionRuleResponse>>(
            new AdminCommissionRulesQuery(page, pageSize), ct));
    }
}

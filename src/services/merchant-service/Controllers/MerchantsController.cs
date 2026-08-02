using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using MerchantService.Application.Commands;
using MerchantService.Application.Queries;
using MerchantService.Domain.Enums;
using MerchantService.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MerchantService.Controllers;

/// <summary>
/// 商户 API — 入驻申请 / 我的商户 / 管理（审核、列表）。
/// </summary>
[ApiController]
[Route("api/merchants")]
[Produces("application/json")]
public sealed class MerchantsController(IMediator mediator) : ControllerBase
{
    /// <summary>提交入驻申请（需登录，一个用户仅一条未终态申请）</summary>
    /// <param name="request">入驻申请请求（商户名 + 营业执照 + 联系人）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 商户申请记录（Pending）；409 — 名称占用或重复申请；401 — 未登录</returns>
    [Authorize]
    [HttpPost("apply")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MerchantResponse>> Apply([FromBody] MerchantApplyRequest request, CancellationToken ct)
    {
        try
        {
            var command = new ApplyMerchantCommand(
                request.Name, request.LicenseNo, request.ContactName,
                request.ContactPhone, request.ContactEmail, request.Description);
            return Created("", await mediator.SendAsync<ApplyMerchantCommand, MerchantResponse>(command, ct));
        }
        catch (DomainException ex) when (ex.ErrorCode is "NAME_EXISTS" or "DUPLICATE_APPLICATION")
        {
            return Conflict(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>我的商户 — 当前登录用户的商户申请状态（需登录）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 商户信息（无申请返回 null）；401 — 未登录</returns>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MerchantResponse?>> Me(CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.QueryAsync<GetMyMerchantQuery, MerchantResponse?>(new GetMyMerchantQuery(), ct));
        }
        catch (DomainException ex) when (ex.ErrorCode == "UNAUTHENTICATED")
        {
            return Unauthorized(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>商户列表（管理员，分页 + 状态过滤）</summary>
    /// <param name="status">按状态过滤（可选：1待审 2通过 3驳回 4禁用）</param>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 分页商户列表；401 — 未登录；403 — 非管理员</returns>
    [Authorize(Roles = "admin")]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<MerchantResponse>>> List(
        [FromQuery] MerchantStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new ListMerchantsQuery(status, page, pageSize);
        return Ok(await mediator.QueryAsync<ListMerchantsQuery, PagedResult<MerchantResponse>>(query, ct));
    }

    /// <summary>商户详情（管理员）</summary>
    /// <param name="id">商户 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 商户信息；404 — 商户不存在</returns>
    [Authorize(Roles = "admin")]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MerchantResponse>> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.QueryAsync<GetMerchantByIdQuery, MerchantResponse>(new GetMerchantByIdQuery(id), ct));
        }
        catch (NotFoundException)
        {
            return NotFound("商户不存在");
        }
    }

    /// <summary>审核商户（管理员，仅限 Pending 状态）</summary>
    /// <param name="id">商户 ID</param>
    /// <param name="request">审核请求（Approved + 驳回原因）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 审核后的商户信息；400 — 驳回缺原因或状态不允许；404 — 商户不存在</returns>
    [Authorize(Roles = "admin")]
    [HttpPost("{id:guid}/review")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MerchantResponse>> Review(Guid id, [FromBody] MerchantReviewRequest request, CancellationToken ct)
    {
        try
        {
            var command = new ReviewMerchantCommand(id, request.Approved, request.Reason);
            return Ok(await mediator.SendAsync<ReviewMerchantCommand, MerchantResponse>(command, ct));
        }
        catch (NotFoundException)
        {
            return NotFound("商户不存在");
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }
}

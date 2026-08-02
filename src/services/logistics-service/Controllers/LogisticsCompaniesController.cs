using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using LogisticsService.Application.Commands;
using LogisticsService.Application.Queries;
using LogisticsService.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogisticsService.Controllers;

/// <summary>
/// 物流公司管理接口（平台端）— 公司 CRUD / 启用停用，需 admin 角色。
/// </summary>
[ApiController]
[Authorize(Roles = "admin")]
[Route("api/logistics/companies")]
[Produces("application/json")]
public sealed class LogisticsCompaniesController(IMediator mediator) : ControllerBase
{
    /// <summary>物流公司列表（含停用，分页）</summary>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 20，上限 100）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 公司分页列表</returns>
    /// <response code="200">公司列表</response>
    /// <response code="401">未登录</response>
    /// <response code="403">非 admin</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<CompanyResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        return Ok(await mediator.QueryAsync<CompanyListQuery, PagedResult<CompanyResponse>>(
            new CompanyListQuery(page, pageSize), ct));
    }

    /// <summary>创建物流公司</summary>
    /// <param name="request">公司信息（编码 + 名称）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 公司；400 — 编码重复或参数非法</returns>
    /// <response code="201">创建成功</response>
    /// <response code="400">编码重复或参数非法</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CompanyResponse>> Create([FromBody] SaveCompanyRequest request, CancellationToken ct)
    {
        try
        {
            return Created("", await mediator.SendAsync<CreateCompanyCommand, CompanyResponse>(
                new CreateCompanyCommand(request), ct));
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>更新物流公司（名称 / 查询链接）</summary>
    /// <param name="id">公司 ID</param>
    /// <param name="request">公司信息</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的公司；404 — 公司不存在</returns>
    /// <response code="200">更新成功</response>
    /// <response code="404">公司不存在</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyResponse>> Update(
        Guid id, [FromBody] SaveCompanyRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<UpdateCompanyCommand, CompanyResponse>(
                new UpdateCompanyCommand(id, request), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "物流公司不存在" });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>启用/停用物流公司</summary>
    /// <param name="id">公司 ID</param>
    /// <param name="request">目标状态</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的公司；404 — 公司不存在</returns>
    /// <response code="200">更新成功</response>
    /// <response code="404">公司不存在</response>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyResponse>> ToggleStatus(
        Guid id, [FromBody] ToggleCompanyRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.SendAsync<ToggleCompanyCommand, CompanyResponse>(
                new ToggleCompanyCommand(id, request.Enabled), ct));
        }
        catch (NotFoundException)
        {
            return NotFound(new { error = "物流公司不存在" });
        }
    }
}

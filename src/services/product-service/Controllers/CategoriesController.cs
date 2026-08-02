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
/// 分类 API — 商户分类管理（父子层级，需请求头 X-Merchant-Id）。
/// </summary>
[ApiController]
[Authorize]
[Route("api/categories")]
[Produces("application/json")]
public sealed class CategoriesController(IMediator mediator) : ControllerBase
{
    /// <summary>创建分类（需登录 + X-Merchant-Id 请求头）</summary>
    /// <param name="request">分类请求（名称 + 父分类 + 排序）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>201 — 分类记录；400 — 缺商户上下文；409 — 同级重名</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> Create([FromBody] CategoryRequest request, CancellationToken ct)
    {
        try
        {
            var command = new CreateCategoryCommand(request.Name, request.ParentId, request.SortOrder);
            return Created("", await mediator.SendAsync<CreateCategoryCommand, CategoryResponse>(command, ct));
        }
        catch (DomainException ex) when (ex.ErrorCode == "NAME_EXISTS")
        {
            return Conflict(new { error = ex.Message, code = ex.ErrorCode });
        }
        catch (DomainException ex) when (ex.ErrorCode == "MERCHANT_REQUIRED")
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>分类列表（当前商户）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 分类列表（按排序）</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> List(CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.QueryAsync<ListCategoriesQuery, IReadOnlyList<CategoryResponse>>(new ListCategoriesQuery(), ct));
        }
        catch (DomainException ex) when (ex.ErrorCode == "MERCHANT_REQUIRED")
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }

    /// <summary>更新分类（名称/层级/排序/启用状态）</summary>
    /// <param name="id">分类 ID</param>
    /// <param name="request">分类请求</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 更新后的分类；400 — 父分类设置错误；404 — 分类不存在</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> Update(Guid id, [FromBody] CategoryRequest request, CancellationToken ct)
    {
        try
        {
            var command = new UpdateCategoryCommand(id, request.Name, request.ParentId, request.SortOrder, request.IsActive ?? true);
            return Ok(await mediator.SendAsync<UpdateCategoryCommand, CategoryResponse>(command, ct));
        }
        catch (NotFoundException)
        {
            return NotFound("分类不存在");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>删除分类（有商品或子分类时禁止）</summary>
    /// <param name="id">分类 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>204 — 已删除；400 — 存在商品/子分类；404 — 分类不存在</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await mediator.SendAsync<DeleteCategoryCommand, Unit>(new DeleteCategoryCommand(id), ct);
            return NoContent();
        }
        catch (NotFoundException)
        {
            return NotFound("分类不存在");
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.ErrorCode });
        }
    }
}

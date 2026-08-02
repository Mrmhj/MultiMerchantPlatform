using BuildingBlocks.Core.CQRS;
using SearchService.Application.Commands;
using SearchService.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace SearchService.Controllers;

/// <summary>
/// 搜索索引内部维护接口 — 仅供 product-service 通过 X-Internal-Key 调用，不对外暴露。
/// </summary>
[ApiController]
[Route("api/search/internal")]
public sealed class InternalSearchController(IMediator mediator, IConfiguration configuration) : ControllerBase
{
    /// <summary>内部密钥校验（X-Internal-Key）</summary>
    private bool KeyValid => Request.Headers["X-Internal-Key"].FirstOrDefault() == configuration["Internal:Key"];

    /// <summary>upsert 搜索索引（商品创建/更新/上下架时同步）</summary>
    /// <param name="request">索引数据</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>是否成功</returns>
    /// <response code="200">同步成功</response>
    /// <response code="401">内部密钥错误</response>
    [HttpPost("upsert")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Upsert([FromBody] UpsertSearchIndexRequest request, CancellationToken ct)
    {
        if (!KeyValid)
            return Unauthorized(new { error = "内部密钥无效" });
        var ok = await mediator.SendAsync<UpsertSearchIndexCommand, bool>(new UpsertSearchIndexCommand(request), ct);
        return Ok(ok);
    }

    /// <summary>移除搜索索引（商品删除时同步）</summary>
    /// <param name="request">商品 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>是否成功</returns>
    /// <response code="200">移除成功</response>
    /// <response code="401">内部密钥错误</response>
    [HttpPost("remove")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Remove([FromBody] RemoveSearchIndexRequest request, CancellationToken ct)
    {
        if (!KeyValid)
            return Unauthorized(new { error = "内部密钥无效" });
        var ok = await mediator.SendAsync<RemoveSearchIndexCommand, bool>(new RemoveSearchIndexCommand(request.ProductId), ct);
        return Ok(ok);
    }
}

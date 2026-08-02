using ProductService.Domain.Enums;
using ProductService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProductService.Controllers;

/// <summary>
/// 商品内部统计接口 — 仅供其他服务通过 X-Internal-Key 调用，不对外暴露。
/// </summary>
[ApiController]
[Route("api/products/internal")]
public sealed class InternalProductsController(ProductDbContext db, IConfiguration configuration) : ControllerBase
{
    /// <summary>内部密钥校验（X-Internal-Key）</summary>
    private bool KeyValid => Request.Headers["X-Internal-Key"].FirstOrDefault() == configuration["Internal:Key"];

    /// <summary>内部商品统计（bi-admin 服务聚合数据源，X-Internal-Key 校验）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 商品总数与在售数量；401 — 内部密钥错误</returns>
    /// <response code="200">商品统计（total/onSale）</response>
    /// <response code="401">内部密钥错误</response>
    [HttpGet("stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        if (!KeyValid)
            return Unauthorized(new { error = "内部密钥无效" });

        var total = await db.Products.AsNoTracking().CountAsync(ct);
        var onSale = await db.Products.AsNoTracking()
            .CountAsync(p => p.Status == ProductStatus.OnSale, ct);

        return Ok(new { total, onSale });
    }
}

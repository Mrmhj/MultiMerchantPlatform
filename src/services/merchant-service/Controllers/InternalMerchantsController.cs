using MerchantService.Domain.Enums;
using MerchantService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MerchantService.Controllers;

/// <summary>
/// 商户内部查询接口 — 仅供其他服务通过 X-Internal-Key 调用，不对外暴露。
/// </summary>
[ApiController]
[Route("api/merchants/internal")]
public sealed class InternalMerchantsController(MerchantDbContext db, IConfiguration configuration) : ControllerBase
{
    /// <summary>内部密钥校验（X-Internal-Key）</summary>
    private bool KeyValid => Request.Headers["X-Internal-Key"].FirstOrDefault() == configuration["Internal:Key"];

    /// <summary>按商户 ID 查询商户名称与状态（product-service 搜索索引同步用）</summary>
    /// <param name="merchantId">商户 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>商户名称与状态</returns>
    /// <response code="200">查询成功</response>
    /// <response code="401">内部密钥错误</response>
    /// <response code="404">商户不存在</response>
    [HttpGet("{merchantId:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetName(Guid merchantId, CancellationToken ct)
    {
        if (!KeyValid)
            return Unauthorized(new { error = "内部密钥无效" });

        var merchant = await db.Merchants.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == merchantId, ct);
        if (merchant is null)
            return NotFound(new { error = "商户不存在" });

        return Ok(new { merchantId = merchant.Id, name = merchant.Name, status = (int)merchant.Status });
    }

    /// <summary>内部商户统计（bi-admin 服务聚合数据源，X-Internal-Key 校验）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>200 — 商户总数与各状态数量；401 — 内部密钥错误</returns>
    /// <response code="200">商户统计（total/approved/pending）</response>
    /// <response code="401">内部密钥错误</response>
    [HttpGet("stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        if (!KeyValid)
            return Unauthorized(new { error = "内部密钥无效" });

        var total = await db.Merchants.AsNoTracking().CountAsync(ct);
        var approved = await db.Merchants.AsNoTracking()
            .CountAsync(m => m.Status == MerchantStatus.Approved, ct);
        var pending = await db.Merchants.AsNoTracking()
            .CountAsync(m => m.Status == MerchantStatus.Pending, ct);

        return Ok(new { total, approved, pending });
    }
}

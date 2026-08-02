using BuildingBlocks.Communication;
using BuildingBlocks.Core.Results;

namespace OrderService.Infrastructure;

/// <summary>
/// 库存服务客户端 — 订单-库存联动（下单预占 / 支付扣减 / 取消释放）。
/// X-Internal-Key 默认头在 DI 注册的命名 HttpClient 中配置。
/// </summary>
public sealed class StockServiceClient(IServiceClient serviceClient)
{
    /// <summary>预占库存（下单时调用，库存不足返回失败）</summary>
    /// <param name="skuId">SKU ID</param>
    /// <param name="quantity">数量</param>
    /// <param name="referenceId">关联订单号</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>操作结果（IsSuccess=false 表示库存不足等失败）</returns>
    public async Task<Result<StockOpResponse>> ReserveAsync(Guid skuId, int quantity, string referenceId, CancellationToken ct = default)
        => await serviceClient.PostAsync<StockOpResponse>(
            "/api/stocks/internal/reserve",
            new { skuId, quantity, referenceId }, ct);

    /// <summary>确认扣减（支付成功时调用）</summary>
    /// <param name="skuId">SKU ID</param>
    /// <param name="quantity">数量</param>
    /// <param name="referenceId">关联订单号</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<Result<StockOpResponse>> ConfirmAsync(Guid skuId, int quantity, string referenceId, CancellationToken ct = default)
        => await serviceClient.PostAsync<StockOpResponse>(
            "/api/stocks/internal/confirm",
            new { skuId, quantity, referenceId }, ct);

    /// <summary>释放预占（取消订单时调用）</summary>
    /// <param name="skuId">SKU ID</param>
    /// <param name="quantity">数量</param>
    /// <param name="referenceId">关联订单号</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<Result<StockOpResponse>> ReleaseAsync(Guid skuId, int quantity, string referenceId, CancellationToken ct = default)
        => await serviceClient.PostAsync<StockOpResponse>(
            "/api/stocks/internal/release",
            new { skuId, quantity, referenceId }, ct);
}

/// <summary>库存操作响应（对应 stock-service 内部接口返回）</summary>
public sealed record StockOpResponse(bool Success, string? Error, object? Stock);

using BuildingBlocks.Communication;

namespace BiAdminService.Infrastructure;

/// <summary>BI 内部取数客户端 — 通过 X-Internal-Key 调用各服务内部接口，聚合为 BI 数据源。</summary>
public sealed class BiDataClients(IHttpClientFactory factory)
{
    private readonly IServiceClient _order = new HttpServiceClient(factory.CreateClient("order"));
    private readonly IServiceClient _merchant = new HttpServiceClient(factory.CreateClient("merchant"));
    private readonly IServiceClient _product = new HttpServiceClient(factory.CreateClient("product"));
    private readonly IServiceClient _identity = new HttpServiceClient(factory.CreateClient("identity"));

    /// <summary>拉取订单 BI 统计（order-service internal/bi-stats）</summary>
    /// <param name="start">周期开始（UTC，可选）</param>
    /// <param name="end">周期结束（UTC，可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>订单统计（失败返回 null）</returns>
    public async Task<OrderBiStatsDto?> GetOrderStatsAsync(DateTime? start, DateTime? end, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (start.HasValue)
            query.Add($"start={start.Value:O}");
        if (end.HasValue)
            query.Add($"end={end.Value:O}");
        var path = "/api/orders/internal/bi-stats" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);

        var result = await _order.GetAsync<OrderBiStatsDto>(path, ct);
        return result.IsSuccess ? result.Value : null;
    }

    /// <summary>拉取商户统计（merchant-service internal/stats）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>商户统计（失败返回 null）</returns>
    public async Task<MerchantStatsDto?> GetMerchantStatsAsync(CancellationToken ct = default)
    {
        var result = await _merchant.GetAsync<MerchantStatsDto>("/api/merchants/internal/stats", ct);
        return result.IsSuccess ? result.Value : null;
    }

    /// <summary>拉取商品统计（product-service internal/stats）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>商品统计（失败返回 null）</returns>
    public async Task<ProductStatsDto?> GetProductStatsAsync(CancellationToken ct = default)
    {
        var result = await _product.GetAsync<ProductStatsDto>("/api/products/internal/stats", ct);
        return result.IsSuccess ? result.Value : null;
    }

    /// <summary>拉取用户统计（identity-service internal/stats）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>用户统计（失败返回 null）</returns>
    public async Task<UserStatsDto?> GetUserStatsAsync(CancellationToken ct = default)
    {
        var result = await _identity.GetAsync<UserStatsDto>("/api/users/internal/stats", ct);
        return result.IsSuccess ? result.Value : null;
    }
}

/// <summary>订单 BI 统计 DTO（order-service internal/bi-stats 返回）</summary>
public sealed record OrderBiStatsDto(
    decimal TotalGmv,
    int TotalOrderCount,
    int PaidOrderCount,
    int CompletedOrderCount,
    List<DailySalesDto> DailySales,
    List<MerchantRankDto> MerchantRank,
    List<ProductRankDto> ProductRank,
    List<OrderStatusDto> OrderStatus);

/// <summary>按天销售点</summary>
public sealed record DailySalesDto(string Date, decimal Gmv, int OrderCount);

/// <summary>商户排行项</summary>
public sealed record MerchantRankDto(Guid MerchantId, string MerchantName, decimal Gmv, int OrderCount);

/// <summary>商品排行项</summary>
public sealed record ProductRankDto(Guid ProductId, string ProductName, int Quantity, decimal Amount);

/// <summary>订单状态分布项</summary>
public sealed record OrderStatusDto(int Status, int Count);

/// <summary>商户统计 DTO</summary>
public sealed record MerchantStatsDto(int Total, int Approved, int Pending);

/// <summary>商品统计 DTO</summary>
public sealed record ProductStatsDto(int Total, int OnSale);

/// <summary>用户统计 DTO</summary>
public sealed record UserStatsDto(int Total);

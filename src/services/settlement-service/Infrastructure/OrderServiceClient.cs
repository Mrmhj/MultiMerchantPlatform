using BuildingBlocks.Communication;

namespace SettlementService.Infrastructure;

/// <summary>
/// 订单服务客户端 — 拉取已完成子订单（结算单生成的数据源）。
/// X-Internal-Key 默认头在 DI 注册的命名 HttpClient "order" 中配置。
/// </summary>
public sealed class OrderServiceClient(IHttpClientFactory factory)
{
    private readonly IServiceClient _client = new HttpServiceClient(factory.CreateClient("order"));

    /// <summary>拉取已完成子订单（结算生成数据源，失败返回空列表不阻断主流程）</summary>
    /// <param name="start">周期开始（可选）</param>
    /// <param name="end">周期结束（可选）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>已完成子订单列表（失败返回空列表）</returns>
    public async Task<List<CompletedSubOrderDto>> GetCompletedSubOrdersAsync(
        DateTime? start, DateTime? end, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (start.HasValue)
            query.Add($"start={start.Value:O}");
        if (end.HasValue)
            query.Add($"end={end.Value:O}");

        var path = "/api/orders/internal/completed-suborders"
            + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);

        var result = await _client.GetAsync<List<CompletedSubOrderDto>>(path, ct);
        return result.IsSuccess ? result.Value ?? [] : [];
    }
}

/// <summary>已完成子订单 DTO（order-service 内部接口返回）</summary>
public sealed record CompletedSubOrderDto(
    Guid SubOrderId,
    Guid OrderId,
    string OrderNo,
    Guid MerchantId,
    string MerchantName,
    decimal TotalAmount,
    DateTime CompletedAt);

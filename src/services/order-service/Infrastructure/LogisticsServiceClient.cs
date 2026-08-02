using BuildingBlocks.Communication;
using BuildingBlocks.Core.Results;

namespace OrderService.Infrastructure;

/// <summary>
/// 物流服务客户端 — 商户发货后创建运单（发货成功 → 物流运单，订单-物流联动）。
/// X-Internal-Key 默认头在 DI 注册的命名 HttpClient "logistics" 中配置。
/// </summary>
public sealed class LogisticsServiceClient(IHttpClientFactory factory)
{
    private readonly IServiceClient _client = new HttpServiceClient(factory.CreateClient("logistics"));

    /// <summary>创建运单（发货成功后调用，失败不阻断发货主流程）</summary>
    /// <param name="buyerUserId">买家用户 ID</param>
    /// <param name="merchantId">商户 ID</param>
    /// <param name="subOrderId">子订单 ID</param>
    /// <param name="orderId">主订单 ID</param>
    /// <param name="orderNo">订单号</param>
    /// <param name="carrierCode">物流公司编码</param>
    /// <param name="trackingNo">运单号</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>操作结果（IsSuccess=false 表示运单创建失败）</returns>
    public async Task<Result<LogisticsShipmentResponse>> CreateShipmentAsync(
        Guid buyerUserId, Guid merchantId, Guid subOrderId, Guid orderId, string orderNo,
        string carrierCode, string trackingNo, CancellationToken ct = default)
        => await _client.PostAsync<LogisticsShipmentResponse>(
            "/api/logistics/internal/shipments",
            new { buyerUserId, merchantId, subOrderId, orderId, orderNo, carrierCode, trackingNo }, ct);
}

/// <summary>物流运单响应（logistics-service 内部接口返回）</summary>
public sealed record LogisticsShipmentResponse(Guid Id, Guid SubOrderId, string TrackingNo, int Status);

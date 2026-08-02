using BuildingBlocks.Communication;
using BuildingBlocks.Core.Results;

namespace PayService.Infrastructure;

/// <summary>
/// 订单服务客户端 — 支付成功后通知 order-service 确认订单已支付（服务间同步调用）。
/// X-Internal-Key 默认头在 DI 注册的命名 HttpClient 中配置。
/// </summary>
public sealed class OrderServiceClient(IServiceClient serviceClient)
{
    /// <summary>通知订单已支付（调用 order-service 内部确认端点）</summary>
    /// <param name="orderId">订单 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>调用结果（IsSuccess 表示订单确认成功）</returns>
    public async Task<Result<object>> ConfirmPaidAsync(Guid orderId, CancellationToken ct = default)
        => await serviceClient.PostAsync<object>($"/api/orders/{orderId}/pay-internal", new { }, ct);
}

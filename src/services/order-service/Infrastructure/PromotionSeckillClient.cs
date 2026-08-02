using BuildingBlocks.Communication;
using BuildingBlocks.Core.Results;

namespace OrderService.Infrastructure;

/// <summary>
/// 促销服务客户端 — 秒杀记录标记订单已创建回调（X-Internal-Key 默认头在 DI 注册的命名 HttpClient "promotion" 中配置；
/// 注入 IHttpClientFactory 按名取，避免多个命名 HttpClient 注册同一 IServiceClient 互相覆盖）。
/// </summary>
public sealed class PromotionSeckillClient(IHttpClientFactory factory)
{
    private readonly IServiceClient _client = new HttpServiceClient(factory.CreateClient("promotion"));
    /// <summary>标记秒杀记录订单已创建</summary>
    /// <param name="seckillRecordId">秒杀记录 ID</param>
    /// <param name="orderId">订单 ID</param>
    /// <param name="orderNo">订单号</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>是否成功</returns>
    public async Task<bool> MarkOrderedAsync(Guid seckillRecordId, Guid orderId, string orderNo, CancellationToken ct = default)
    {
        var result = await _client.PutAsync<object>(
            $"/api/promotion/seckills/internal/{seckillRecordId}/order",
            new { orderId, orderNo }, ct);
        return result.IsSuccess;
    }
}

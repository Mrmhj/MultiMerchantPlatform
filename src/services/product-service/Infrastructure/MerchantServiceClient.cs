using BuildingBlocks.Communication;

namespace ProductService.Infrastructure;

/// <summary>
/// 商户服务客户端 — 查询商户名称（搜索索引同步用）。
/// X-Internal-Key 默认头在 DI 注册的命名 HttpClient "merchant" 中配置。
/// </summary>
public sealed class MerchantServiceClient(IHttpClientFactory factory)
{
    private readonly IServiceClient _client = new HttpServiceClient(factory.CreateClient("merchant"));

    /// <summary>按商户 ID 查询商户名称</summary>
    /// <param name="merchantId">商户 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>商户名称（失败返回 null，不阻塞主流程）</returns>
    public async Task<string?> GetNameAsync(Guid merchantId, CancellationToken ct = default)
    {
        var result = await _client.GetAsync<MerchantNameResponse>(
            $"/api/merchants/internal/{merchantId}", ct);
        return result.IsSuccess ? result.Value?.Name : null;
    }
}

/// <summary>商户名称响应（merchant-service 内部接口返回）</summary>
public sealed record MerchantNameResponse(Guid MerchantId, string Name, int Status);

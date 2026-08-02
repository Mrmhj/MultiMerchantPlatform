using BuildingBlocks.Communication;
using BuildingBlocks.Core.Results;

namespace ProductService.Infrastructure;

/// <summary>
/// 搜索服务客户端 — 商品变更时同步搜索索引（upsert/remove）。
/// 同步失败仅记日志，不阻塞商品主流程（索引最终一致）。
/// X-Internal-Key 默认头在 DI 注册的命名 HttpClient "search" 中配置。
/// </summary>
public sealed class SearchServiceClient(IHttpClientFactory factory)
{
    private readonly IServiceClient _client = new HttpServiceClient(factory.CreateClient("search"));

    /// <summary>upsert 搜索索引（创建/更新/上下架时调用）</summary>
    /// <param name="productId">商品 ID</param>
    /// <param name="merchantId">商户 ID</param>
    /// <param name="merchantName">商户名称</param>
    /// <param name="name">商品名称</param>
    /// <param name="description">商品描述</param>
    /// <param name="categoryId">分类 ID</param>
    /// <param name="categoryName">分类名称</param>
    /// <param name="coverImage">封面图 URL</param>
    /// <param name="priceMin">最低价</param>
    /// <param name="priceMax">最高价</param>
    /// <param name="status">商品状态</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<Result<bool>> UpsertAsync(Guid productId, Guid merchantId, string merchantName,
        string name, string? description, Guid categoryId, string categoryName, string? coverImage,
        decimal priceMin, decimal priceMax, int status, CancellationToken ct = default)
        => await _client.PostAsync<bool>("/api/search/internal/upsert", new
        {
            productId,
            merchantId,
            merchantName,
            name,
            description,
            categoryId,
            categoryName,
            coverImage,
            priceMin,
            priceMax,
            status,
        }, ct);

    /// <summary>移除搜索索引（商品删除时调用）</summary>
    /// <param name="productId">商品 ID</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<Result<bool>> RemoveAsync(Guid productId, CancellationToken ct = default)
        => await _client.PostAsync<bool>("/api/search/internal/remove", new { productId }, ct);
}
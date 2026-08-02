namespace ProductService.Application;

/// <summary>
/// 商品缓存键规范（Redis/In-Memory 共用）。
/// 列表缓存带版本号：写操作自增版本 → 所有分页列表整体失效（无需枚举 key）。
/// </summary>
public static class ProductCacheKeys
{
    /// <summary>C 端商品详情前缀</summary>
    public const string DetailPrefix = "product:public:detail:";

    /// <summary>C 端商品列表版本键（写操作自增，驱动列表缓存整体失效）</summary>
    public const string ListVersionKey = "product:public:list:version";

    /// <summary>C 端商品详情键</summary>
    /// <param name="productId">商品 ID</param>
    /// <returns>缓存键</returns>
    public static string Detail(Guid productId) => $"{DetailPrefix}{productId:N}";

    /// <summary>C 端商品列表键（带版本号）</summary>
    /// <param name="page">页码</param>
    /// <param name="pageSize">页大小</param>
    /// <param name="version">列表版本号</param>
    /// <returns>缓存键</returns>
    public static string List(int page, int pageSize, long version)
        => $"product:public:list:v{version}:{page}:{pageSize}";
}

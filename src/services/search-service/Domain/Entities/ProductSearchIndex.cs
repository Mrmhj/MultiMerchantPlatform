using BuildingBlocks.Core.Entities;

namespace SearchService.Domain.Entities;

/// <summary>
/// 商品搜索索引 — 从 product-service 同步的商品快照（搜索专用投影）。
/// 由内部接口 upsert/remove 维护，C 端搜索只查此表。
/// </summary>
public sealed class ProductSearchIndex : Entity
{
    private ProductSearchIndex() { } // EF Core

    /// <summary>创建索引记录</summary>
    /// <param name="productId">商品 ID（业务主键，唯一）</param>
    /// <param name="merchantId">所属商户 ID</param>
    /// <param name="merchantName">商户名称</param>
    /// <param name="name">商品名称</param>
    /// <param name="description">商品描述</param>
    /// <param name="categoryId">分类 ID</param>
    /// <param name="categoryName">分类名称</param>
    /// <param name="coverImage">封面图 URL</param>
    /// <param name="priceMin">最低 SKU 价</param>
    /// <param name="priceMax">最高 SKU 价</param>
    /// <param name="status">商品状态（2=在售）</param>
    public ProductSearchIndex(Guid productId, Guid merchantId, string merchantName, string name,
        string? description, Guid categoryId, string categoryName, string? coverImage,
        decimal priceMin, decimal priceMax, int status)
    {
        ProductId = productId;
        MerchantId = merchantId;
        MerchantName = merchantName;
        Name = name;
        Description = description;
        CategoryId = categoryId;
        CategoryName = categoryName;
        CoverImage = coverImage;
        PriceMin = priceMin;
        PriceMax = priceMax;
        Status = status;
    }

    /// <summary>商品 ID（业务主键，唯一）</summary>
    public Guid ProductId { get; private set; }

    /// <summary>所属商户 ID</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>商户名称</summary>
    public string MerchantName { get; private set; } = string.Empty;

    /// <summary>商品名称</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>商品描述（搜索字段）</summary>
    public string? Description { get; private set; }

    /// <summary>分类 ID</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>分类名称</summary>
    public string CategoryName { get; private set; } = string.Empty;

    /// <summary>封面图 URL</summary>
    public string? CoverImage { get; private set; }

    /// <summary>最低 SKU 价</summary>
    public decimal PriceMin { get; private set; }

    /// <summary>最高 SKU 价</summary>
    public decimal PriceMax { get; private set; }

    /// <summary>商品状态（ProductStatus：2=OnSale）</summary>
    public int Status { get; private set; }

    /// <summary>全量更新索引（商品变更时调用）</summary>
    /// <param name="merchantName">商户名称</param>
    /// <param name="name">商品名称</param>
    /// <param name="description">商品描述</param>
    /// <param name="categoryId">分类 ID</param>
    /// <param name="categoryName">分类名称</param>
    /// <param name="coverImage">封面图 URL</param>
    /// <param name="priceMin">最低价</param>
    /// <param name="priceMax">最高价</param>
    /// <param name="status">商品状态</param>
    public void Update(string merchantName, string name, string? description, Guid categoryId,
        string categoryName, string? coverImage, decimal priceMin, decimal priceMax, int status)
    {
        MerchantName = merchantName;
        Name = name;
        Description = description;
        CategoryId = categoryId;
        CategoryName = categoryName;
        CoverImage = coverImage;
        PriceMin = priceMin;
        PriceMax = priceMax;
        Status = status;
    }
}

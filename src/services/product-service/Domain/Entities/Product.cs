using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Entities;
using ProductService.Domain.Enums;

namespace ProductService.Domain.Entities;

/// <summary>
/// 商品实体 — 商户商品（含多 SKU）。
/// 状态机（Draft/OnSale/OffSale）与 SKU 管理内聚（充血模型）；多租户实体，MerchantId 隔离。
/// </summary>
public sealed class Product : MultiTenantEntity
{
    private readonly List<ProductSku> _skus = [];

    private Product() { } // EF Core

    /// <summary>创建商品（初始状态 Draft）</summary>
    /// <param name="merchantId">所属商户 ID</param>
    /// <param name="name">商品名称</param>
    /// <param name="categoryId">分类 ID</param>
    /// <param name="description">商品描述（可选）</param>
    /// <param name="coverImage">封面图 URL（可选）</param>
    [SetsRequiredMembers]
    public Product(Guid merchantId, string name, Guid categoryId, string? description = null, string? coverImage = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        MerchantId = merchantId;
        Name = name.Trim();
        CategoryId = categoryId;
        Description = description?.Trim();
        CoverImage = coverImage?.Trim();
        Status = ProductStatus.Draft;
    }

    /// <summary>商品名称</summary>
    public string Name { get; private set; } = null!;

    /// <summary>分类 ID</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>商品描述</summary>
    public string? Description { get; private set; }

    /// <summary>封面图 URL</summary>
    public string? CoverImage { get; private set; }

    /// <summary>商品状态（Draft/OnSale/OffSale）</summary>
    public ProductStatus Status { get; private set; }

    /// <summary>SKU 列表（规格 × 价格 × 库存）</summary>
    public IReadOnlyList<ProductSku> Skus => _skus;

    /// <summary>添加 SKU（草稿态或更新态）</summary>
    /// <param name="skuCode">SKU 编码（商户内唯一）</param>
    /// <param name="spec">规格描述（如 500g）</param>
    /// <param name="price">售价（元）</param>
    /// <param name="stock">初始库存</param>
    public void AddSku(string skuCode, string spec, decimal price, int stock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skuCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(spec);
        if (price < 0)
            throw new ArgumentException("价格不能为负", nameof(price));
        if (stock < 0)
            throw new ArgumentException("库存不能为负", nameof(stock));

        if (_skus.Any(s => s.SkuCode == skuCode))
            throw new InvalidOperationException($"SKU 编码已存在：{skuCode}");

        _skus.Add(new ProductSku(Id, skuCode.Trim(), spec.Trim(), price, stock));
    }

    /// <summary>更新商品基本信息（名称/分类/描述/封面）</summary>
    /// <param name="name">新名称</param>
    /// <param name="categoryId">新分类 ID</param>
    /// <param name="description">新描述</param>
    /// <param name="coverImage">新封面</param>
    public void UpdateInfo(string name, Guid categoryId, string? description = null, string? coverImage = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        CategoryId = categoryId;
        Description = description?.Trim();
        CoverImage = coverImage?.Trim();
    }

    /// <summary>上架 — 至少需要一个启用且价格有效的 SKU</summary>
    public void Publish()
    {
        if (!_skus.Any(s => s.IsActive))
            throw new InvalidOperationException("上架前至少需要一个启用的 SKU");

        Status = ProductStatus.OnSale;
    }

    /// <summary>下架</summary>
    public void Unpublish() => Status = ProductStatus.OffSale;
}

/// <summary>
/// 商品 SKU — 规格单元（编码/规格/价格/库存）。
/// </summary>
public sealed class ProductSku : Entity
{
    private ProductSku() { } // EF Core

    /// <summary>创建 SKU</summary>
    /// <param name="productId">所属商品 ID</param>
    /// <param name="skuCode">SKU 编码（商户内唯一）</param>
    /// <param name="spec">规格描述</param>
    /// <param name="price">售价（元）</param>
    /// <param name="stock">库存数量</param>
    public ProductSku(Guid productId, string skuCode, string spec, decimal price, int stock)
    {
        ProductId = productId;
        SkuCode = skuCode;
        Spec = spec;
        Price = price;
        Stock = stock;
        IsActive = true;
    }

    /// <summary>所属商品 ID</summary>
    public Guid ProductId { get; private set; }

    /// <summary>SKU 编码（商户内唯一）</summary>
    public string SkuCode { get; private set; } = null!;

    /// <summary>规格描述（如 500g / 1kg）</summary>
    public string Spec { get; private set; } = null!;

    /// <summary>售价（元）</summary>
    public decimal Price { get; private set; }

    /// <summary>库存数量</summary>
    public int Stock { get; private set; }

    /// <summary>是否启用</summary>
    public bool IsActive { get; private set; }

    /// <summary>更新价格与库存</summary>
    /// <param name="price">新价格</param>
    /// <param name="stock">新库存</param>
    public void Update(decimal price, int stock)
    {
        if (price < 0)
            throw new ArgumentException("价格不能为负", nameof(price));
        if (stock < 0)
            throw new ArgumentException("库存不能为负", nameof(stock));

        Price = price;
        Stock = stock;
    }

    /// <summary>启用 SKU</summary>
    public void Activate() => IsActive = true;

    /// <summary>停用 SKU</summary>
    public void Deactivate() => IsActive = false;
}

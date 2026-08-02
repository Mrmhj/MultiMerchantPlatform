using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;

namespace CartService.Domain.Entities;

/// <summary>
/// 购物车条目 — 买家维度隔离（UserId 归属），同一 SKU 自动合并数量。
/// 商品/价格以下单时快照为准，购物车仅作为预选清单。
/// </summary>
public sealed class CartItem : Entity
{
    private CartItem() { } // EF Core

    /// <summary>创建购物车条目</summary>
    /// <param name="userId">买家用户 ID</param>
    /// <param name="merchantId">所属商户 ID</param>
    /// <param name="merchantName">商户名称</param>
    /// <param name="productId">商品 ID</param>
    /// <param name="productName">商品名称</param>
    /// <param name="skuId">SKU ID</param>
    /// <param name="skuCode">SKU 编码</param>
    /// <param name="spec">规格描述</param>
    /// <param name="unitPrice">单价</param>
    /// <param name="quantity">数量</param>
    public CartItem(Guid userId, Guid merchantId, string merchantName, Guid productId, string productName,
        Guid skuId, string skuCode, string spec, decimal unitPrice, int quantity)
    {
        UserId = userId;
        MerchantId = merchantId;
        MerchantName = merchantName;
        ProductId = productId;
        ProductName = productName;
        SkuId = skuId;
        SkuCode = skuCode;
        Spec = spec;
        UnitPrice = unitPrice;
        ChangeQuantity(quantity);
    }

    /// <summary>买家用户 ID（隔离维度）</summary>
    public Guid UserId { get; private set; }

    /// <summary>所属商户 ID</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>商户名称（快照）</summary>
    public string MerchantName { get; private set; } = string.Empty;

    /// <summary>商品 ID</summary>
    public Guid ProductId { get; private set; }

    /// <summary>商品名称（快照）</summary>
    public string ProductName { get; private set; } = string.Empty;

    /// <summary>SKU ID</summary>
    public Guid SkuId { get; private set; }

    /// <summary>SKU 编码（快照）</summary>
    public string SkuCode { get; private set; } = string.Empty;

    /// <summary>规格描述（快照）</summary>
    public string Spec { get; private set; } = string.Empty;

    /// <summary>单价（快照，下单时以订单实际价为准）</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>数量（1-999）</summary>
    public int Quantity { get; private set; }

    /// <summary>是否选中（默认选中，结算取选中项）</summary>
    public bool IsSelected { get; private set; } = true;

    /// <summary>变更数量（含合并加购）</summary>
    /// <param name="quantity">新数量（1-999）</param>
    public void ChangeQuantity(int quantity)
    {
        if (quantity is < 1 or > 999)
            throw new DomainException("数量需在 1-999 之间", "INVALID_QUANTITY");
        Quantity = quantity;
    }

    /// <summary>设置选中状态</summary>
    /// <param name="selected">是否选中</param>
    public void Select(bool selected) => IsSelected = selected;
}

namespace ProductService.Domain.Enums;

/// <summary>
/// 商品状态。
/// </summary>
public enum ProductStatus
{
    /// <summary>草稿（未上架）</summary>
    Draft = 1,

    /// <summary>在售</summary>
    OnSale = 2,

    /// <summary>已下架</summary>
    OffSale = 3
}

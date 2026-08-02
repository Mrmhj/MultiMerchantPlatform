using BuildingBlocks.Core.Entities;

namespace BiAdminService.Domain.Entities;

/// <summary>
/// BI 总览快照 — 平台核心指标（单行聚合，同步时刷新）。
/// </summary>
public sealed class BiOverview : Entity
{
    /// <summary>创建空总览（仅用于首条种子记录）</summary>
    public BiOverview()
    {
        TotalGmv = 0m;
        TotalOrders = 0;
        PaidOrders = 0;
        CompletedOrders = 0;
        MerchantCount = 0;
        ProductCount = 0;
        UserCount = 0;
        SyncedAt = DateTime.UtcNow;
    }

    /// <summary>累计 GMV（元，已付款子单合计）</summary>
    public decimal TotalGmv { get; private set; }

    /// <summary>主订单总数</summary>
    public int TotalOrders { get; private set; }

    /// <summary>已付款主订单数（含已完成）</summary>
    public int PaidOrders { get; private set; }

    /// <summary>已完成主订单数</summary>
    public int CompletedOrders { get; private set; }

    /// <summary>商户总数</summary>
    public int MerchantCount { get; private set; }

    /// <summary>商品总数</summary>
    public int ProductCount { get; private set; }

    /// <summary>注册用户总数</summary>
    public int UserCount { get; private set; }

    /// <summary>最近一次同步时间（UTC）</summary>
    public DateTime SyncedAt { get; private set; }

    /// <summary>刷新总览快照（同步时整体覆盖）</summary>
    /// <param name="totalGmv">累计 GMV</param>
    /// <param name="totalOrders">主订单总数</param>
    /// <param name="paidOrders">已付款订单数</param>
    /// <param name="completedOrders">已完成订单数</param>
    /// <param name="merchantCount">商户总数</param>
    /// <param name="productCount">商品总数</param>
    /// <param name="userCount">用户总数</param>
    public void Refresh(decimal totalGmv, int totalOrders, int paidOrders, int completedOrders,
        int merchantCount, int productCount, int userCount)
    {
        TotalGmv = totalGmv;
        TotalOrders = totalOrders;
        PaidOrders = paidOrders;
        CompletedOrders = completedOrders;
        MerchantCount = merchantCount;
        ProductCount = productCount;
        UserCount = userCount;
        SyncedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// BI 按天销售聚合 — 销售趋势折线数据源（同步时重建）。
/// </summary>
public sealed class BiDailySales : Entity
{
    private BiDailySales() { } // EF Core

    /// <summary>创建按天销售记录</summary>
    /// <param name="date">日期（UTC 日）</param>
    /// <param name="gmv">当日 GMV（元）</param>
    /// <param name="orderCount">当日订单数</param>
    public BiDailySales(DateTime date, decimal gmv, int orderCount)
    {
        Date = date.Date;
        Gmv = gmv;
        OrderCount = orderCount;
    }

    /// <summary>日期（UTC）</summary>
    public DateTime Date { get; private set; }

    /// <summary>当日 GMV（元）</summary>
    public decimal Gmv { get; private set; }

    /// <summary>当日订单数</summary>
    public int OrderCount { get; private set; }
}

/// <summary>
/// BI 商户销售排行聚合 — 商户排行柱状图数据源（同步时重建）。
/// </summary>
public sealed class BiMerchantSales : Entity
{
    private BiMerchantSales() { } // EF Core

    /// <summary>创建商户销售排行记录</summary>
    /// <param name="merchantId">商户 ID</param>
    /// <param name="merchantName">商户名称（快照）</param>
    /// <param name="gmv">累计 GMV（元）</param>
    /// <param name="orderCount">订单数</param>
    public BiMerchantSales(Guid merchantId, string merchantName, decimal gmv, int orderCount)
    {
        MerchantId = merchantId;
        MerchantName = merchantName;
        Gmv = gmv;
        OrderCount = orderCount;
    }

    /// <summary>商户 ID</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>商户名称（快照）</summary>
    public string MerchantName { get; private set; } = null!;

    /// <summary>累计 GMV（元）</summary>
    public decimal Gmv { get; private set; }

    /// <summary>订单数</summary>
    public int OrderCount { get; private set; }
}

/// <summary>
/// BI 商品销售排行聚合 — 商品排行柱状图数据源（同步时重建）。
/// </summary>
public sealed class BiProductSales : Entity
{
    private BiProductSales() { } // EF Core

    /// <summary>创建商品销售排行记录</summary>
    /// <param name="productId">商品 ID</param>
    /// <param name="productName">商品名称（快照）</param>
    /// <param name="quantity">累计销量</param>
    /// <param name="amount">累计销售额（元）</param>
    public BiProductSales(Guid productId, string productName, int quantity, decimal amount)
    {
        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        Amount = amount;
    }

    /// <summary>商品 ID</summary>
    public Guid ProductId { get; private set; }

    /// <summary>商品名称（快照）</summary>
    public string ProductName { get; private set; } = null!;

    /// <summary>累计销量</summary>
    public int Quantity { get; private set; }

    /// <summary>累计销售额（元）</summary>
    public decimal Amount { get; private set; }
}

/// <summary>
/// BI 主订单状态分布 — 状态饼图数据源（同步时重建）。
/// </summary>
public sealed class BiOrderStatusDist : Entity
{
    private BiOrderStatusDist() { } // EF Core

    /// <summary>创建状态分布记录</summary>
    /// <param name="status">主订单状态值（1待付款 2已付款 3已完成 4已取消）</param>
    /// <param name="count">数量</param>
    public BiOrderStatusDist(int status, int count)
    {
        Status = status;
        Count = count;
    }

    /// <summary>主订单状态值（1待付款 2已付款 3已完成 4已取消）</summary>
    public int Status { get; private set; }

    /// <summary>数量</summary>
    public int Count { get; private set; }
}

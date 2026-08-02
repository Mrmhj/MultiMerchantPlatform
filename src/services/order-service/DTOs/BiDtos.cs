namespace OrderService.DTOs;

/// <summary>BI 按天销售点（日期 + GMV + 订单数）</summary>
public sealed record BiDailySalesPoint(string Date, decimal Gmv, int OrderCount);

/// <summary>BI 商户销售排行项</summary>
public sealed record BiMerchantRankItem(Guid MerchantId, string MerchantName, decimal Gmv, int OrderCount);

/// <summary>BI 商品销售排行项</summary>
public sealed record BiProductRankItem(Guid ProductId, string ProductName, int Quantity, decimal Amount);

/// <summary>BI 订单状态分布项（主订单状态值 + 数量）</summary>
public sealed record BiOrderStatusItem(int Status, int Count);

/// <summary>
/// BI 订单统计响应 — 内部接口（X-Internal-Key）供 bi-admin 服务聚合使用。
/// 销售口径：子订单状态为 Paid/Shipped/Completed（已付款即计入 GMV）。
/// </summary>
public sealed record BiOrderStatsResponse(
    decimal TotalGmv,
    int TotalOrderCount,
    int PaidOrderCount,
    int CompletedOrderCount,
    List<BiDailySalesPoint> DailySales,
    List<BiMerchantRankItem> MerchantRank,
    List<BiProductRankItem> ProductRank,
    List<BiOrderStatusItem> OrderStatus);

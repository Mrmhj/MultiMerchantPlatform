namespace BiAdminService.DTOs;

/// <summary>总览指标卡响应（web-admin BI 看板核心指标）</summary>
public sealed record BiOverviewResponse(
    decimal TotalGmv,
    int TotalOrders,
    int PaidOrders,
    int CompletedOrders,
    int MerchantCount,
    int ProductCount,
    int UserCount,
    DateTime SyncedAt);

/// <summary>按天销售趋势点</summary>
public sealed record BiSalesTrendPoint(string Date, decimal Gmv, int OrderCount);

/// <summary>商户销售排行项</summary>
public sealed record BiMerchantRankResponse(Guid MerchantId, string MerchantName, decimal Gmv, int OrderCount);

/// <summary>商品销售排行项</summary>
public sealed record BiProductRankResponse(Guid ProductId, string ProductName, int Quantity, decimal Amount);

/// <summary>订单状态分布项</summary>
public sealed record BiOrderStatusResponse(int Status, int Count);

/// <summary>同步执行结果响应</summary>
public sealed record BiSyncResponse(
    bool Success,
    string? Error,
    int DailySales,
    int MerchantRows,
    int ProductRows,
    int StatusRows,
    int MerchantCount,
    int ProductCount,
    int UserCount,
    decimal TotalGmv,
    int TotalOrders,
    DateTime SyncedAt);

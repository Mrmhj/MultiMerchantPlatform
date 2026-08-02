using BiAdminService.Domain.Entities;
using BiAdminService.Infrastructure;
using BiAdminService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BiAdminService.Application.Services;

/// <summary>
/// BI 数据同步服务 — 拉取各服务内部统计接口，重建聚合表（整体覆盖，幂等）。
/// </summary>
public sealed class BiSyncService(BiDbContext db, BiDataClients clients)
{
    /// <summary>执行一次全量同步（重建聚合表）</summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>同步结果（各来源是否成功 + 更新条数）</returns>
    public async Task<BiSyncResult> SyncAsync(CancellationToken ct = default)
    {
        var order = await clients.GetOrderStatsAsync(null, null, ct);
        var merchant = await clients.GetMerchantStatsAsync(ct);
        var product = await clients.GetProductStatsAsync(ct);
        var user = await clients.GetUserStatsAsync(ct);

        if (order is null)
            return BiSyncResult.Failed("order-service 取数失败");

        // 重建聚合表（事务内整体覆盖）
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await db.DailySales.ExecuteDeleteAsync(ct);
        await db.MerchantSales.ExecuteDeleteAsync(ct);
        await db.ProductSales.ExecuteDeleteAsync(ct);
        await db.OrderStatusDist.ExecuteDeleteAsync(ct);

        db.DailySales.AddRange(order.DailySales.Select(d => new BiDailySales(
            DateTime.Parse(d.Date), d.Gmv, d.OrderCount)));
        db.MerchantSales.AddRange(order.MerchantRank.Select(m => new BiMerchantSales(
            m.MerchantId, m.MerchantName, m.Gmv, m.OrderCount)));
        db.ProductSales.AddRange(order.ProductRank.Select(p => new BiProductSales(
            p.ProductId, p.ProductName, p.Quantity, p.Amount)));
        db.OrderStatusDist.AddRange(order.OrderStatus.Select(s => new BiOrderStatusDist(s.Status, s.Count)));

        // 总览快照（单行 upsert）
        var overview = await db.Overviews.AsNoTracking().FirstOrDefaultAsync(ct);
        if (overview is null)
        {
            var fresh = new BiOverview();
            fresh.Refresh(
                order.TotalGmv, order.TotalOrderCount, order.PaidOrderCount, order.CompletedOrderCount,
                merchant?.Total ?? 0, product?.Total ?? 0, user?.Total ?? 0);
            db.Overviews.Add(fresh);
        }
        else
        {
            var tracked = await db.Overviews.FirstOrDefaultAsync(ct);
            tracked!.Refresh(
                order.TotalGmv, order.TotalOrderCount, order.PaidOrderCount, order.CompletedOrderCount,
                merchant?.Total ?? 0, product?.Total ?? 0, user?.Total ?? 0);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new BiSyncResult(true, null,
            order.DailySales.Count, order.MerchantRank.Count,
            order.ProductRank.Count, order.OrderStatus.Count,
            merchant?.Total ?? 0, product?.Total ?? 0, user?.Total ?? 0,
            order.TotalGmv, order.TotalOrderCount);
    }
}

/// <summary>BI 同步结果</summary>
public sealed record BiSyncResult(
    bool Success,
    string? Error,
    int DailySales = 0,
    int MerchantRows = 0,
    int ProductRows = 0,
    int StatusRows = 0,
    int MerchantCount = 0,
    int ProductCount = 0,
    int UserCount = 0,
    decimal TotalGmv = 0m,
    int TotalOrders = 0)
{
    /// <summary>构造失败结果</summary>
    /// <param name="error">失败原因</param>
    /// <returns>失败结果</returns>
    public static BiSyncResult Failed(string error) => new(false, error);
}

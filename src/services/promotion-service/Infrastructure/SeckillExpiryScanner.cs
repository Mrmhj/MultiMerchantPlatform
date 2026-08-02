using BuildingBlocks.Cache;
using PromotionService.Application.Commands;
using PromotionService.Domain.Enums;
using PromotionService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PromotionService.Infrastructure;

/// <summary>
/// 秒杀超时回收后台任务 — 周期扫描超时未支付（Pending 超过 ExpireAt）的秒杀记录：
/// 回补 Redis 秒杀库存 + 标记 Expired（替代延迟消息方案，本地可靠无需消息总线支持）。
/// </summary>
public sealed class SeckillExpiryScanner(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<SeckillExpiryScanner> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("秒杀超时回收任务已启动，周期 {Interval}s", Interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "秒杀超时回收扫描异常");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PromotionDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var expired = await db.SeckillRecords
            .Where(r => r.Status == SeckillRecordStatus.Pending && r.ExpireAt <= now)
            .Take(200)
            .ToListAsync(ct);

        if (expired.Count == 0)
            return;

        foreach (var record in expired)
        {
            // 回补 Redis 库存（活动库存键仍存在才回补）
            var stockKey = SeckillCacheKeys.StockKey(record.ActivityId);
            await cache.IncrementAsync(stockKey, record.Quantity, ct);
            record.MarkExpired();
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("秒杀超时回收 {Count} 条记录，库存已回补", expired.Count);
    }
}

using BuildingBlocks.Cache;
using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Events;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Core.Results;
using BuildingBlocks.Messaging;
using BuildingBlocks.MultiTenant;
using PromotionService.Domain.Entities;
using PromotionService.Domain.Enums;
using PromotionService.DTOs;
using PromotionService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PromotionService.Application.Commands;

/// <summary>创建秒杀活动命令（商户端）</summary>
/// <param name="Request">秒杀活动信息</param>
public sealed record CreateSeckillCommand(CreateSeckillRequest Request) : ICommand<SeckillResponse>;

/// <summary>创建秒杀活动命令处理器</summary>
public sealed class CreateSeckillCommandHandler(
    PromotionDbContext db,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider) : ICommandHandler<CreateSeckillCommand, SeckillResponse>
{
    /// <inheritdoc />
    public async Task<SeckillResponse> HandleAsync(CreateSeckillCommand command, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var r = command.Request;
        var activity = new SeckillActivity(
            merchantId, r.MerchantName, r.Name,
            r.ProductId, r.ProductName,
            r.SkuId, r.SkuCode, r.Spec,
            r.SeckillPrice, r.TotalStock, r.LimitPerUser,
            r.StartTime, r.EndTime);
        db.SeckillActivities.Add(activity);
        await db.SaveChangesAsync(ct);

        return PromotionMapper.ToSeckillResponse(activity, timeProvider.GetUtcNow().UtcDateTime);
    }
}

/// <summary>变更秒杀活动状态命令（启用/停用）</summary>
/// <param name="Id">秒杀活动 ID</param>
/// <param name="Active">目标状态：true 启用 / false 停用</param>
public sealed record ChangeSeckillStatusCommand(Guid Id, bool Active) : ICommand<SeckillResponse>;

/// <summary>变更秒杀活动状态命令处理器 — 启用时预热 Redis 库存（缓存预扣前置）</summary>
public sealed class ChangeSeckillStatusCommandHandler(
    PromotionDbContext db,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider,
    ICacheService cache) : ICommandHandler<ChangeSeckillStatusCommand, SeckillResponse>
{
    /// <inheritdoc />
    public async Task<SeckillResponse> HandleAsync(ChangeSeckillStatusCommand command, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var activity = await db.SeckillActivities.FirstOrDefaultAsync(
            a => a.Id == command.Id && a.MerchantId == merchantId, ct)
            ?? throw new NotFoundException("秒杀活动", command.Id);

        if (command.Active)
        {
            activity.Activate(now);
            // 预热秒杀库存到缓存（Redis 原子预扣，防超卖）
            await cache.SetAsync(SeckillCacheKeys.StockKey(activity.Id), (long)activity.TotalStock, null, ct);
        }
        else
        {
            activity.Disable();
            // 停用移除预热库存（Redis 键清理，避免残留）
            await cache.RemoveAsync(SeckillCacheKeys.StockKey(activity.Id), ct);
        }

        // 活动启停影响 C 端进行中列表 → 主动失效
        await cache.RemoveAsync(SeckillCacheKeys.ActiveListKey, ct);

        await db.SaveChangesAsync(ct);
        return PromotionMapper.ToSeckillResponse(activity, now);
    }
}

/// <summary>秒杀抢购命令（买家端）— 缓存预扣 + 落秒杀记录（异步下单由 order-service 消费消息完成）</summary>
/// <param name="UserId">买家用户 ID</param>
/// <param name="ActivityId">秒杀活动 ID</param>
/// <param name="Quantity">购买数量</param>
public sealed record BuySeckillCommand(Guid UserId, Guid ActivityId, int Quantity)
    : ICommand<BuySeckillResult>;

/// <summary>秒杀抢购命令处理器 — 校验活动 → 限购 → Redis 原子预扣（防超卖）→ 落秒杀记录 → 发布异步下单消息</summary>
public sealed class BuySeckillCommandHandler(
    PromotionDbContext db,
    TimeProvider timeProvider,
    ICacheService cache,
    IDistributedLock distributedLock,
    IMessagePublisher messagePublisher,
    ILogger<BuySeckillCommandHandler> logger) : ICommandHandler<BuySeckillCommand, BuySeckillResult>
{
    /// <inheritdoc />
    public async Task<BuySeckillResult> HandleAsync(BuySeckillCommand command, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // ① 校验活动（存在 + Active + 时间窗口内）
        var activity = await db.SeckillActivities.AsNoTracking().FirstOrDefaultAsync(
            a => a.Id == command.ActivityId, ct);
        if (activity is null)
            return Fail("秒杀活动不存在");
        if (activity.Status != SeckillStatus.Active || !activity.InPeriodAt(now))
            return Fail("秒杀未开始或已结束");

        // ② 每人限购校验（该用户活动内已购数量 + 本次数量 ≤ 限购）
        var bought = await db.SeckillRecords.CountAsync(
            r => r.ActivityId == command.ActivityId && r.UserId == command.UserId
                 && r.Status == SeckillRecordStatus.Pending, ct);
        if (bought + command.Quantity > activity.LimitPerUser)
            return Fail("超过该活动每人限购数量");

        // ③ 分布式锁（防同一活动并发预扣竞争，短锁快速释放）
        using var lockHandle = await distributedLock.TryAcquireAsync(
            SeckillCacheKeys.LockKey(activity.Id), TimeSpan.FromSeconds(5), ct);
        if (lockHandle is null)
            return Fail("系统繁忙，请稍后重试");

        // ④ 缓存原子预扣（TryDeductAsync：不足返回 false，不超卖）
        var stockKey = SeckillCacheKeys.StockKey(activity.Id);
        if (!await cache.TryDeductAsync(stockKey, command.Quantity, ct))
            return Fail("秒杀库存不足，已售罄");

        // ⑤ 落秒杀记录（Pending，等待异步下单）
        var expireAt = now.AddMinutes(15); // 15 分钟未支付回滚库存
        var record = new SeckillRecord(
            activity.Id, activity.MerchantId, activity.MerchantName, command.UserId,
            activity.ProductId, activity.ProductName,
            activity.SkuId, activity.SkuCode, activity.Spec,
            activity.SeckillPrice, command.Quantity, expireAt);
        db.SeckillRecords.Add(record);
        await db.SaveChangesAsync(ct);

        // ⑥ 发布异步下单消息（order-service 消费创建订单；失败仅记录日志，由秒杀记录超时回收兜底）
        try
        {
            var orderEvent = new SeckillOrderRequestedEvent
            {
                RecordId = record.Id,
                ActivityId = activity.Id,
                MerchantId = activity.MerchantId,
                MerchantName = activity.MerchantName,
                UserId = command.UserId,
                ProductId = activity.ProductId,
                ProductName = activity.ProductName,
                SkuId = activity.SkuId,
                SkuCode = activity.SkuCode,
                Spec = activity.Spec,
                UnitPrice = activity.SeckillPrice,
                Quantity = command.Quantity,
                ExpireAt = expireAt,
            };
            await messagePublisher.PublishAsync(orderEvent, ct: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "秒杀下单消息发布失败 RecordId={RecordId}，等待超时回收", record.Id);
        }

        return new BuySeckillResult
        {
            Success = true,
            RecordId = record.Id,
            ActivityId = activity.Id,
            UnitPrice = activity.SeckillPrice,
            Quantity = command.Quantity,
            ExpireAt = expireAt,
        };
    }

    private static BuySeckillResult Fail(string error)
        => new() { Success = false, Error = error };
}

/// <summary>秒杀记录标记订单已创建命令（order-service 内部回调，X-Internal-Key）</summary>
/// <param name="RecordId">秒杀记录 ID</param>
/// <param name="OrderId">订单 ID</param>
/// <param name="OrderNo">订单号</param>
public sealed record MarkSeckillOrderedCommand(Guid RecordId, Guid OrderId, string OrderNo)
    : ICommand<Result<SeckillRecordResponse>>;

/// <summary>秒杀记录标记订单已创建命令处理器（幂等：仅 Pending 可流转）</summary>
public sealed class MarkSeckillOrderedCommandHandler(
    PromotionDbContext db) : ICommandHandler<MarkSeckillOrderedCommand, Result<SeckillRecordResponse>>
{
    /// <inheritdoc />
    public async Task<Result<SeckillRecordResponse>> HandleAsync(
        MarkSeckillOrderedCommand command, CancellationToken ct = default)
    {
        var record = await db.SeckillRecords.FirstOrDefaultAsync(r => r.Id == command.RecordId, ct);
        if (record is null)
            return Result.Failure<SeckillRecordResponse>("秒杀记录不存在");

        record.MarkOrdered(command.OrderId, command.OrderNo);
        await db.SaveChangesAsync(ct);
        return Result<SeckillRecordResponse>.Success(PromotionMapper.ToSeckillRecordResponse(record));
    }
}

/// <summary>秒杀缓存键规范</summary>
public static class SeckillCacheKeys
{
    /// <summary>秒杀库存键（缓存原子预扣）</summary>
    public static string StockKey(Guid activityId) => $"seckill:stock:{activityId}";

    /// <summary>秒杀活动分布式锁键</summary>
    public static string LockKey(Guid activityId) => $"seckill:lock:{activityId}";

    /// <summary>C 端进行中秒杀列表键（活动启停时失效）</summary>
    public static string ActiveListKey => "seckill:active:list";
}

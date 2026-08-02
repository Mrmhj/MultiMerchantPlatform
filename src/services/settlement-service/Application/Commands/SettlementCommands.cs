using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using SettlementService.Domain.Entities;
using SettlementService.DTOs;
using SettlementService.Infrastructure;
using SettlementService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SettlementService.Application.Commands;

/// <summary>生成结算单命令（平台端，按周期扫描已完成子订单）</summary>
/// <param name="CycleStart">周期开始（可选）</param>
/// <param name="CycleEnd">周期结束（可选）</param>
public sealed record GenerateSettlementsCommand(DateTime? CycleStart, DateTime? CycleEnd)
    : ICommand<GenerateSettlementResponse>;

/// <summary>
/// 生成结算单命令处理器：
/// 拉取已完成子订单 → 排除已结算 → 按商户聚合 → 按佣金规则计算佣金 → 生成结算单 + 明细。
/// 幂等：子订单 SubOrderId 唯一索引保证一个子订单只结算一次。
/// </summary>
public sealed class GenerateSettlementsCommandHandler(
    SettlementDbContext db,
    OrderServiceClient orderClient,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<GenerateSettlementsCommandHandler> logger) : ICommandHandler<GenerateSettlementsCommand, GenerateSettlementResponse>
{
    /// <inheritdoc />
    public async Task<GenerateSettlementResponse> HandleAsync(GenerateSettlementsCommand command, CancellationToken ct = default)
    {
        var start = command.CycleStart?.ToUniversalTime();
        var end = command.CycleEnd?.ToUniversalTime();

        // 1. 拉取已完成子订单（订单服务）
        var subOrders = await orderClient.GetCompletedSubOrdersAsync(start, end, ct);
        if (subOrders.Count == 0)
            return new GenerateSettlementResponse();

        // 2. 排除已结算的子订单（幂等，防重复结算）
        var subOrderIds = subOrders.Select(s => s.SubOrderId).ToList();
        var settledIds = await db.SettlementItems.AsNoTracking()
            .Where(i => subOrderIds.Contains(i.SubOrderId))
            .Select(i => i.SubOrderId)
            .ToListAsync(ct);
        var pending = subOrders.Where(s => !settledIds.Contains(s.SubOrderId)).ToList();

        // 3. 佣金规则（一次查全，缺省用平台默认）
        var merchantIds = pending.Select(s => s.MerchantId).Distinct().ToList();
        var rules = await db.CommissionRules.AsNoTracking()
            .Where(r => merchantIds.Contains(r.MerchantId))
            .ToDictionaryAsync(r => r.MerchantId, r => r.Rate, ct);
        var defaultRate = configuration.GetValue<decimal>("DefaultCommissionRate", 5m);

        // 4. 按商户分组生成结算单
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var settlements = new List<SettlementResponse>();
        var skipped = 0;

        foreach (var group in pending.GroupBy(s => s.MerchantId))
        {
            var rate = rules.TryGetValue(group.Key, out var r) ? r : defaultRate;
            var settlement = new Settlement(
                group.Key, group.First().MerchantName, start ?? DateTime.MinValue, end ?? DateTime.MaxValue, now);

            foreach (var sub in group)
            {
                settlement.AddItem(sub.SubOrderId, sub.OrderNo, sub.TotalAmount, rate);
            }

            db.Settlements.Add(settlement);
            settlements.Add(SettlementMapper.ToResponse(settlement, includeItems: true));
        }

        skipped = subOrders.Count - pending.Count;
        if (settlements.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
        else
        {
            logger.LogInformation("生成结算单：无新增可结算子订单（已结算 {Skipped} 条）", skipped);
        }

        return new GenerateSettlementResponse { Settlements = settlements, SkippedCount = skipped };
    }
}

/// <summary>确认结算命令（平台端，Pending → Settled）</summary>
/// <param name="SettlementId">结算单 ID</param>
public sealed record SettleSettlementCommand(Guid SettlementId) : ICommand<SettlementResponse>;

/// <summary>确认结算命令处理器</summary>
public sealed class SettleSettlementCommandHandler(
    SettlementDbContext db,
    TimeProvider timeProvider) : ICommandHandler<SettleSettlementCommand, SettlementResponse>
{
    /// <inheritdoc />
    public async Task<SettlementResponse> HandleAsync(SettleSettlementCommand command, CancellationToken ct = default)
    {
        var settlement = await db.Settlements
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == command.SettlementId, ct)
            ?? throw new NotFoundException("结算单", command.SettlementId);

        settlement.Settle(timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return SettlementMapper.ToResponse(settlement, includeItems: true);
    }
}

/// <summary>标记已打款命令（平台端，Settled → Paid）</summary>
/// <param name="SettlementId">结算单 ID</param>
public sealed record MarkPaidSettlementCommand(Guid SettlementId) : ICommand<SettlementResponse>;

/// <summary>标记已打款命令处理器</summary>
public sealed class MarkPaidSettlementCommandHandler(
    SettlementDbContext db,
    TimeProvider timeProvider) : ICommandHandler<MarkPaidSettlementCommand, SettlementResponse>
{
    /// <inheritdoc />
    public async Task<SettlementResponse> HandleAsync(MarkPaidSettlementCommand command, CancellationToken ct = default)
    {
        var settlement = await db.Settlements
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == command.SettlementId, ct)
            ?? throw new NotFoundException("结算单", command.SettlementId);

        settlement.MarkPaid(timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return SettlementMapper.ToResponse(settlement, includeItems: true);
    }
}

/// <summary>设置/更新佣金规则命令（平台端，一个商户一条规则）</summary>
/// <param name="MerchantId">商户 ID</param>
/// <param name="Rate">佣金比例（0-100）</param>
public sealed record UpsertCommissionRuleCommand(Guid MerchantId, decimal Rate) : ICommand<CommissionRuleResponse>;

/// <summary>设置/更新佣金规则命令处理器（存在则更新，不存在则创建）</summary>
public sealed class UpsertCommissionRuleCommandHandler(
    SettlementDbContext db) : ICommandHandler<UpsertCommissionRuleCommand, CommissionRuleResponse>
{
    /// <inheritdoc />
    public async Task<CommissionRuleResponse> HandleAsync(UpsertCommissionRuleCommand command, CancellationToken ct = default)
    {
        var rule = await db.CommissionRules.FirstOrDefaultAsync(r => r.MerchantId == command.MerchantId, ct);
        if (rule is null)
        {
            rule = new CommissionRule(command.MerchantId, command.Rate);
            db.CommissionRules.Add(rule);
        }
        else
        {
            rule.ChangeRate(command.Rate);
        }

        await db.SaveChangesAsync(ct);
        return SettlementMapper.ToCommissionRuleResponse(rule, isDefault: false);
    }
}

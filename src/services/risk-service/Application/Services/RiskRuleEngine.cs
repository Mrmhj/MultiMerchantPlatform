using RiskService.Domain.Entities;
using RiskService.Domain.Enums;
using RiskService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace RiskService.Application.Services;

/// <summary>
/// 风控规则引擎 — 对上报事件按启用规则做「场景 + 维度 + 时间窗口」聚合统计，超过阈值即命中。
/// 命中策略：
/// 1. 同一规则 + 同一维度键在窗口内已有未处置（Open/Reviewing）案例 → 追加次数（不重复建单）
/// 2. 否则新建案例（Open）
/// 3. 黑名单（未过期且启用）命中 → 生成 BLACKLIST 来源的 Block 案例
/// 统计口径：本批次事件先 Add（未保存），先按 (规则,维度键) 在内存计数，
/// 再叠加数据库窗口内历史计数，保证批量上报同键事件不遗漏。
/// </summary>
public sealed class RiskRuleEngine(RiskDbContext db, TimeProvider timeProvider)
{
    /// <summary>评估一批事件并落库（事务内），返回本次命中案例</summary>
    /// <param name="events">待评估事件</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>本次命中的案例列表</returns>
    public async Task<List<RiskCase>> EvaluateAsync(IReadOnlyList<RiskEvent> events, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var hits = new List<RiskCase>();

        if (events.Count == 0)
            return hits;

        // 1. 加载全部启用规则（一次查全，内存匹配）
        var rules = await db.RiskRules.AsNoTracking()
            .Where(r => r.Enabled)
            .ToListAsync(ct);

        // 2. 黑名单（启用且未过期）
        var blacklist = await db.BlacklistEntries.AsNoTracking()
            .Where(b => b.Enabled)
            .ToListAsync(ct);
        var effectiveBlacklist = blacklist.Where(b => !b.IsExpired(now)).ToList();

        // 3. 事件入上下文（未保存）—— 供 5.1 的内存计数使用
        foreach (var evt in events)
            await db.RiskEvents.AddAsync(evt, ct);

        // 4. 黑名单拦截（逐事件，10 分钟内去重）
        foreach (var evt in events)
            await EvaluateBlacklistAsync(evt, effectiveBlacklist, now, hits, ct);

        // 5. 规则命中（按 规则×维度键 分组统计，避免批量同键重复查库）
        await EvaluateRulesAsync(events, rules, now, hits, ct);

        await db.SaveChangesAsync(ct);
        return hits;
    }

    /// <summary>黑名单评估：用户/IP/设备任一命中 → Block 案例（10 分钟窗口内去重）</summary>
    private async Task EvaluateBlacklistAsync(RiskEvent evt, List<BlacklistEntry> blacklist,
        DateTime now, List<RiskCase> hits, CancellationToken ct)
    {
        foreach (var entry in blacklist.Where(b => MatchesBlacklist(b, evt)))
        {
            var dimension = entry.TargetType switch
            {
                BlacklistTargetType.User => RiskDimension.User,
                BlacklistTargetType.Device => RiskDimension.Device,
                _ => RiskDimension.Ip,
            };
            var key = entry.TargetValue;
            if (!await HasOpenCaseAsync(null, dimension, key, now, ct))
            {
                var riskCase = new RiskCase(
                    ruleId: null,
                    ruleName: "黑名单拦截",
                    scene: evt.Scene,
                    dimension: dimension,
                    dimensionKey: key,
                    userId: evt.UserId,
                    merchantId: entry.MerchantId ?? evt.MerchantId,
                    ip: evt.Ip,
                    deviceId: evt.DeviceId,
                    occurredCount: 1,
                    threshold: 1,
                    disposition: RiskDisposition.Block,
                    source: "BLACKLIST",
                    summary: $"黑名单拦截（{DescribeTarget(entry.TargetType)}: {entry.TargetValue}）— {entry.Reason}");
                db.RiskCases.Add(riskCase);
                hits.Add(riskCase);
            }
        }
    }

    /// <summary>规则评估：匹配规则的 (场景, 维度键) 组合，窗口内总次数 = 本批内存计数 + 库内历史计数</summary>
    private async Task EvaluateRulesAsync(IReadOnlyList<RiskEvent> events, List<RiskRule> rules,
        DateTime now, List<RiskCase> hits, CancellationToken ct)
    {

        // 5.1 本批内存计数：(ruleId, dimensionKey) → count
        var batchCounts = new Dictionary<(Guid RuleId, string Key), int>();
        foreach (var evt in events)
        {
            foreach (var rule in rules)
            {
                if (!string.Equals(rule.Scene, evt.Scene, StringComparison.OrdinalIgnoreCase)
                    || (rule.MerchantId != null && rule.MerchantId != evt.MerchantId))
                    continue;

                var key = GetDimensionKey(rule.Dimension, evt);
                if (key is null)
                    continue;

                var k = (rule.Id, key);
                batchCounts[k] = batchCounts.GetValueOrDefault(k) + 1;
            }
        }

        if (batchCounts.Count == 0)
            return;

        // 5.2 按 (规则, 维度键) 计算窗口内总次数并判定命中
        foreach (var ((ruleId, key), batchCount) in batchCounts)
        {
            var rule = rules.First(r => r.Id == ruleId);
            var since = now.AddSeconds(-rule.WindowSeconds);

            // 库内窗口计数（不含本批；数据库精确统计，含本次语义由 batchCount 补齐）
            // 维度匹配按 Dimension 分支内联表达式（EF 可翻译）
            var dbCount = rule.Dimension switch
            {
                RiskDimension.User => await db.RiskEvents.AsNoTracking()
                    .CountAsync(e => e.Scene == rule.Scene
                        && e.OccurredAt >= since && e.OccurredAt <= now
                        && e.UserId != null && e.UserId.ToString() == key, ct),
                RiskDimension.Ip => await db.RiskEvents.AsNoTracking()
                    .CountAsync(e => e.Scene == rule.Scene
                        && e.OccurredAt >= since && e.OccurredAt <= now
                        && e.Ip != null && e.Ip == key, ct),
                RiskDimension.Device => await db.RiskEvents.AsNoTracking()
                    .CountAsync(e => e.Scene == rule.Scene
                        && e.OccurredAt >= since && e.OccurredAt <= now
                        && e.DeviceId != null && e.DeviceId == key, ct),
                RiskDimension.Merchant => await db.RiskEvents.AsNoTracking()
                    .CountAsync(e => e.Scene == rule.Scene
                        && e.OccurredAt >= since && e.OccurredAt <= now
                        && e.MerchantId != null && e.MerchantId.ToString() == key, ct),
                _ => 0,
            };

            var total = dbCount + batchCount;
            if (total < rule.Threshold)
                continue;

            // 命中：同规则 + 同维度键 + 窗口内未处置案例 → 追加，否则新建
            var existing = await db.RiskCases.AsNoTracking()
                .FirstOrDefaultAsync(c => c.RuleId == rule.Id
                    && c.DimensionKey == key
                    && (c.Status == RiskCaseStatus.Open || c.Status == RiskCaseStatus.Reviewing)
                    && c.CreatedAt >= since, ct);

            if (existing is not null)
            {
                existing.IncreaseCount(total - existing.OccurredCount);
                db.RiskCases.Update(existing);
                hits.Add(existing);
            }
            else
            {
                var sample = events.FirstOrDefault(e =>
                    string.Equals(e.Scene, rule.Scene, StringComparison.OrdinalIgnoreCase)
                    && (rule.MerchantId == null || rule.MerchantId == e.MerchantId)
                    && GetDimensionKey(rule.Dimension, e) == key);

                var riskCase = new RiskCase(
                    ruleId: rule.Id,
                    ruleName: rule.Name,
                    scene: rule.Scene,
                    dimension: rule.Dimension,
                    dimensionKey: key,
                    userId: sample?.UserId,
                    merchantId: sample?.MerchantId,
                    ip: sample?.Ip,
                    deviceId: sample?.DeviceId,
                    occurredCount: total,
                    threshold: rule.Threshold,
                    disposition: rule.Disposition,
                    source: "RULE_HIT",
                    summary: $"{rule.Name}：{rule.WindowSeconds}秒内{DescribeDimension(rule.Dimension)}达到 {total} 次（阈值 {rule.Threshold}）");
                db.RiskCases.Add(riskCase);
                hits.Add(riskCase);
            }
        }
    }

    /// <summary>查询同键窗口内是否已有未处置案例（黑名单拦截去重用）</summary>
    private Task<bool> HasOpenCaseAsync(Guid? ruleId, RiskDimension dimension, string key, DateTime now, CancellationToken ct)
    {
        var since = now.AddMinutes(-10); // 黑名单拦截 10 分钟内去重
        return db.RiskCases.AsNoTracking()
            .AnyAsync(c => c.RuleId == ruleId
                && c.Dimension == dimension
                && c.DimensionKey == key
                && (c.Status == RiskCaseStatus.Open || c.Status == RiskCaseStatus.Reviewing)
                && c.CreatedAt >= since, ct);
    }

    /// <summary>按规则维度提取维度键</summary>
    private static string? GetDimensionKey(RiskDimension dimension, RiskEvent evt)
        => dimension switch
        {
            RiskDimension.User => evt.UserId?.ToString(),
            RiskDimension.Ip => evt.Ip,
            RiskDimension.Device => evt.DeviceId,
            RiskDimension.Merchant => evt.MerchantId?.ToString(),
            _ => null,
        };

    private static bool MatchesBlacklist(BlacklistEntry entry, RiskEvent evt)
        => entry.TargetType switch
        {
            BlacklistTargetType.User => evt.UserId.ToString() == entry.TargetValue,
            BlacklistTargetType.Ip => evt.Ip == entry.TargetValue,
            BlacklistTargetType.Device => evt.DeviceId == entry.TargetValue,
            _ => false,
        };

    private static string DescribeDimension(RiskDimension dimension)
        => dimension switch
        {
            RiskDimension.User => "该用户操作",
            RiskDimension.Ip => "该 IP 操作",
            RiskDimension.Device => "该设备操作",
            RiskDimension.Merchant => "该商户操作",
            _ => "操作",
        };

    private static string DescribeTarget(BlacklistTargetType type)
        => type switch
        {
            BlacklistTargetType.User => "用户",
            BlacklistTargetType.Ip => "IP",
            BlacklistTargetType.Device => "设备",
            _ => "对象",
        };
}

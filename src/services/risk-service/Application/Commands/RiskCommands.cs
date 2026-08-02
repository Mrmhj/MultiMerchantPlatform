using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using RiskService.Application.Services;
using RiskService.Domain.Entities;
using RiskService.Domain.Enums;
using RiskService.DTOs;
using RiskService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace RiskService.Application.Commands;

/// <summary>批量上报风控事件命令（内部接口，X-Internal-Key）— 落库 + 规则引擎评估</summary>
/// <param name="Events">事件列表</param>
public sealed record SubmitRiskEventsCommand(IReadOnlyList<SubmitRiskEventRequest> Events)
    : ICommand<SubmitRiskEventResponse>;

/// <summary>批量上报风控事件命令处理器</summary>
public sealed class SubmitRiskEventsCommandHandler(
    RiskDbContext db,
    RiskRuleEngine engine,
    ILogger<SubmitRiskEventsCommandHandler> logger) : ICommandHandler<SubmitRiskEventsCommand, SubmitRiskEventResponse>
{
    /// <inheritdoc />
    public async Task<SubmitRiskEventResponse> HandleAsync(SubmitRiskEventsCommand command, CancellationToken ct = default)
    {
        var requests = command.Events ?? [];
        if (requests.Count == 0)
            return new SubmitRiskEventResponse();

        var events = requests.Select(e => new RiskEvent(
            e.Scene, e.UserId, e.MerchantId, e.Ip, e.DeviceId, e.PayloadJson, e.OccurredAt)).ToList();

        var hits = await engine.EvaluateAsync(events, ct);
        logger.LogInformation("风控事件上报：{Count} 条，命中 {Hits} 条", events.Count, hits.Count);

        return new SubmitRiskEventResponse
        {
            Submitted = events.Count,
            Hits = hits.Count,
            Cases = hits.Select(RiskMapper.ToCaseResponse).ToList(),
        };
    }
}

/// <summary>风控决策命令（内部接口，业务方下单/领券前调用）</summary>
/// <param name="Request">决策请求</param>
public sealed record RiskDecisionCommand(RiskDecisionRequest Request) : ICommand<RiskDecisionResponse>;

/// <summary>
/// 风控决策命令处理器：
/// 黑名单命中（用户/IP/设备，启用且未过期）→ 拦截；
/// 存在该用户未处置（Open/Reviewing）的 Block 级案例 → 拦截；否则放行。
/// </summary>
public sealed class RiskDecisionCommandHandler(
    RiskDbContext db,
    TimeProvider timeProvider) : ICommandHandler<RiskDecisionCommand, RiskDecisionResponse>
{
    /// <inheritdoc />
    public async Task<RiskDecisionResponse> HandleAsync(RiskDecisionCommand command, CancellationToken ct = default)
    {
        var request = command.Request;
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // 1. 黑名单检查
        var targetValues = new List<(BlacklistTargetType Type, string? Value)>
        {
            (BlacklistTargetType.User, request.UserId?.ToString()),
            (BlacklistTargetType.Ip, request.Ip),
            (BlacklistTargetType.Device, request.DeviceId),
        };

        foreach (var (type, value) in targetValues)
        {
            if (string.IsNullOrEmpty(value))
                continue;

            var entry = await db.BlacklistEntries.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Enabled
                    && (!b.ExpiresAt.HasValue || b.ExpiresAt.Value > now)
                    && b.TargetType == type && b.TargetValue == value
                    && (b.MerchantId == null || b.MerchantId == request.MerchantId), ct);

            if (entry is not null)
                return new RiskDecisionResponse
                {
                    Allow = false,
                    Reason = $"黑名单拦截（{entry.TargetValue}）：{entry.Reason}",
                    BlacklistId = entry.Id,
                    Disposition = RiskDisposition.Block,
                };
        }

        // 2. 未处置 Block 级案例检查（优先用户维度，其次 IP/设备）
        if (request.UserId.HasValue)
        {
            var userCase = await db.RiskCases.AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == request.UserId
                    && c.Disposition == RiskDisposition.Block
                    && (c.Status == RiskCaseStatus.Open || c.Status == RiskCaseStatus.Reviewing), ct);
            if (userCase is not null)
                return new RiskDecisionResponse
                {
                    Allow = false,
                    Reason = $"命中风控规则：{userCase.Summary}",
                    CaseId = userCase.Id,
                    Disposition = RiskDisposition.Block,
                };
        }

        if (!string.IsNullOrEmpty(request.Ip))
        {
            var ipCase = await db.RiskCases.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Ip == request.Ip
                    && c.Disposition == RiskDisposition.Block
                    && (c.Status == RiskCaseStatus.Open || c.Status == RiskCaseStatus.Reviewing), ct);
            if (ipCase is not null)
                return new RiskDecisionResponse
                {
                    Allow = false,
                    Reason = $"命中风控规则：{ipCase.Summary}",
                    CaseId = ipCase.Id,
                    Disposition = RiskDisposition.Block,
                };
        }

        return new RiskDecisionResponse { Allow = true, Reason = "放行" };
    }
}

/// <summary>创建风控规则命令（平台端）</summary>
/// <param name="Request">规则配置</param>
public sealed record CreateRiskRuleCommand(SaveRiskRuleRequest Request) : ICommand<RiskRuleResponse>;

/// <summary>创建风控规则命令处理器</summary>
public sealed class CreateRiskRuleCommandHandler(
    RiskDbContext db) : ICommandHandler<CreateRiskRuleCommand, RiskRuleResponse>
{
    /// <inheritdoc />
    public async Task<RiskRuleResponse> HandleAsync(CreateRiskRuleCommand command, CancellationToken ct = default)
    {
        var r = command.Request;
        var rule = new RiskRule(r.Name, r.Scene, r.Dimension, r.WindowSeconds, r.Threshold,
            r.Disposition, r.MerchantId, r.Description);
        db.RiskRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return RiskMapper.ToRuleResponse(rule);
    }
}

/// <summary>更新风控规则命令（平台端）</summary>
/// <param name="RuleId">规则 ID</param>
/// <param name="Request">规则配置</param>
public sealed record UpdateRiskRuleCommand(Guid RuleId, SaveRiskRuleRequest Request) : ICommand<RiskRuleResponse>;

/// <summary>更新风控规则命令处理器</summary>
public sealed class UpdateRiskRuleCommandHandler(
    RiskDbContext db) : ICommandHandler<UpdateRiskRuleCommand, RiskRuleResponse>
{
    /// <inheritdoc />
    public async Task<RiskRuleResponse> HandleAsync(UpdateRiskRuleCommand command, CancellationToken ct = default)
    {
        var rule = await db.RiskRules.FirstOrDefaultAsync(r => r.Id == command.RuleId, ct)
            ?? throw new NotFoundException("风控规则", command.RuleId);

        var r = command.Request;
        rule.Update(r.Name, r.Scene, r.Dimension, r.WindowSeconds, r.Threshold,
            r.Disposition, r.MerchantId, r.Description);
        await db.SaveChangesAsync(ct);
        return RiskMapper.ToRuleResponse(rule);
    }
}

/// <summary>删除风控规则命令（平台端）</summary>
/// <param name="RuleId">规则 ID</param>
public sealed record DeleteRiskRuleCommand(Guid RuleId) : ICommand;

/// <summary>删除风控规则命令处理器</summary>
public sealed class DeleteRiskRuleCommandHandler(
    RiskDbContext db) : ICommandHandler<DeleteRiskRuleCommand>
{
    /// <inheritdoc />
    public async Task<Unit> HandleAsync(DeleteRiskRuleCommand command, CancellationToken ct = default)
    {
        var rule = await db.RiskRules.FirstOrDefaultAsync(r => r.Id == command.RuleId, ct)
            ?? throw new NotFoundException("风控规则", command.RuleId);
        db.RiskRules.Remove(rule);
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}

/// <summary>启用/停用风控规则命令（平台端）</summary>
/// <param name="RuleId">规则 ID</param>
/// <param name="Enabled">是否启用</param>
public sealed record SetRiskRuleEnabledCommand(Guid RuleId, bool Enabled) : ICommand<RiskRuleResponse>;

/// <summary>启用/停用风控规则命令处理器</summary>
public sealed class SetRiskRuleEnabledCommandHandler(
    RiskDbContext db) : ICommandHandler<SetRiskRuleEnabledCommand, RiskRuleResponse>
{
    /// <inheritdoc />
    public async Task<RiskRuleResponse> HandleAsync(SetRiskRuleEnabledCommand command, CancellationToken ct = default)
    {
        var rule = await db.RiskRules.FirstOrDefaultAsync(r => r.Id == command.RuleId, ct)
            ?? throw new NotFoundException("风控规则", command.RuleId);

        if (command.Enabled) rule.Enable(); else rule.Disable();
        await db.SaveChangesAsync(ct);
        return RiskMapper.ToRuleResponse(rule);
    }
}

/// <summary>开始复核风险案例命令（平台端，Open → Reviewing）</summary>
/// <param name="CaseId">案例 ID</param>
public sealed record StartReviewRiskCaseCommand(Guid CaseId) : ICommand<RiskCaseResponse>;

/// <summary>开始复核风险案例命令处理器</summary>
public sealed class StartReviewRiskCaseCommandHandler(
    RiskDbContext db) : ICommandHandler<StartReviewRiskCaseCommand, RiskCaseResponse>
{
    /// <inheritdoc />
    public async Task<RiskCaseResponse> HandleAsync(StartReviewRiskCaseCommand command, CancellationToken ct = default)
    {
        var riskCase = await db.RiskCases.FirstOrDefaultAsync(c => c.Id == command.CaseId, ct)
            ?? throw new NotFoundException("风险案例", command.CaseId);
        riskCase.StartReview();
        await db.SaveChangesAsync(ct);
        return RiskMapper.ToCaseResponse(riskCase);
    }
}

/// <summary>确认风险案例命令（平台端，Open/Reviewing → Resolved）</summary>
/// <param name="CaseId">案例 ID</param>
/// <param name="Note">处置备注</param>
public sealed record ResolveRiskCaseCommand(Guid CaseId, string? Note) : ICommand<RiskCaseResponse>;

/// <summary>确认风险案例命令处理器</summary>
public sealed class ResolveRiskCaseCommandHandler(
    RiskDbContext db,
    TimeProvider timeProvider) : ICommandHandler<ResolveRiskCaseCommand, RiskCaseResponse>
{
    /// <inheritdoc />
    public async Task<RiskCaseResponse> HandleAsync(ResolveRiskCaseCommand command, CancellationToken ct = default)
    {
        var riskCase = await db.RiskCases.FirstOrDefaultAsync(c => c.Id == command.CaseId, ct)
            ?? throw new NotFoundException("风险案例", command.CaseId);
        riskCase.Resolve(command.Note, timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return RiskMapper.ToCaseResponse(riskCase);
    }
}

/// <summary>标记误报命令（平台端，Open/Reviewing → FalsePositive）</summary>
/// <param name="CaseId">案例 ID</param>
/// <param name="Note">误报说明</param>
public sealed record MarkFalsePositiveRiskCaseCommand(Guid CaseId, string? Note) : ICommand<RiskCaseResponse>;

/// <summary>标记误报命令处理器</summary>
public sealed class MarkFalsePositiveRiskCaseCommandHandler(
    RiskDbContext db,
    TimeProvider timeProvider) : ICommandHandler<MarkFalsePositiveRiskCaseCommand, RiskCaseResponse>
{
    /// <inheritdoc />
    public async Task<RiskCaseResponse> HandleAsync(MarkFalsePositiveRiskCaseCommand command, CancellationToken ct = default)
    {
        var riskCase = await db.RiskCases.FirstOrDefaultAsync(c => c.Id == command.CaseId, ct)
            ?? throw new NotFoundException("风险案例", command.CaseId);
        riskCase.MarkFalsePositive(command.Note, timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return RiskMapper.ToCaseResponse(riskCase);
    }
}

/// <summary>加入黑名单命令（平台端，同对象已存在则更新原因/有效期）</summary>
/// <param name="Request">黑名单配置</param>
public sealed record AddBlacklistCommand(SaveBlacklistRequest Request) : ICommand<BlacklistResponse>;

/// <summary>加入黑名单命令处理器（存在同对象则更新，否则新增）</summary>
public sealed class AddBlacklistCommandHandler(
    RiskDbContext db,
    TimeProvider timeProvider) : ICommandHandler<AddBlacklistCommand, BlacklistResponse>
{
    /// <inheritdoc />
    public async Task<BlacklistResponse> HandleAsync(AddBlacklistCommand command, CancellationToken ct = default)
    {
        var r = command.Request;
        var existing = await db.BlacklistEntries
            .FirstOrDefaultAsync(b => b.TargetType == r.TargetType
                && b.TargetValue == r.TargetValue.Trim()
                && b.MerchantId == r.MerchantId, ct);

        BlacklistEntry entry;
        if (existing is not null)
        {
            entry = existing;
            entry.Enable();
            // 更新原因与有效期（EF 跟踪实体直接改私有属性不可行，走领域方法）
            entry.Update(r.Reason, r.ExpiresAt);
        }
        else
        {
            entry = new BlacklistEntry(r.TargetType, r.TargetValue, r.Reason, r.ExpiresAt, r.MerchantId);
            db.BlacklistEntries.Add(entry);
        }

        await db.SaveChangesAsync(ct);
        return RiskMapper.ToBlacklistResponse(entry, timeProvider.GetUtcNow().UtcDateTime);
    }
}

/// <summary>移除黑名单命令（平台端，物理删除）</summary>
/// <param name="BlacklistId">黑名单 ID</param>
public sealed record RemoveBlacklistCommand(Guid BlacklistId) : ICommand;

/// <summary>移除黑名单命令处理器</summary>
public sealed class RemoveBlacklistCommandHandler(
    RiskDbContext db) : ICommandHandler<RemoveBlacklistCommand>
{
    /// <inheritdoc />
    public async Task<Unit> HandleAsync(RemoveBlacklistCommand command, CancellationToken ct = default)
    {
        var entry = await db.BlacklistEntries.FirstOrDefaultAsync(b => b.Id == command.BlacklistId, ct)
            ?? throw new NotFoundException("黑名单", command.BlacklistId);
        db.BlacklistEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
        return new Unit();
    }
}

/// <summary>启用/停用黑名单命令（平台端）</summary>
/// <param name="BlacklistId">黑名单 ID</param>
/// <param name="Enabled">是否启用</param>
public sealed record SetBlacklistEnabledCommand(Guid BlacklistId, bool Enabled) : ICommand<BlacklistResponse>;

/// <summary>启用/停用黑名单命令处理器</summary>
public sealed class SetBlacklistEnabledCommandHandler(
    RiskDbContext db,
    TimeProvider timeProvider) : ICommandHandler<SetBlacklistEnabledCommand, BlacklistResponse>
{
    /// <inheritdoc />
    public async Task<BlacklistResponse> HandleAsync(SetBlacklistEnabledCommand command, CancellationToken ct = default)
    {
        var entry = await db.BlacklistEntries.FirstOrDefaultAsync(b => b.Id == command.BlacklistId, ct)
            ?? throw new NotFoundException("黑名单", command.BlacklistId);

        if (command.Enabled) entry.Enable(); else entry.Disable();
        await db.SaveChangesAsync(ct);
        return RiskMapper.ToBlacklistResponse(entry, timeProvider.GetUtcNow().UtcDateTime);
    }
}

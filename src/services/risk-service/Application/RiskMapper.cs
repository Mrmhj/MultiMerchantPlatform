using RiskService.Domain.Entities;
using RiskService.DTOs;

namespace RiskService.Application;

/// <summary>
/// 实体 → DTO 映射。
/// </summary>
public static class RiskMapper
{
    /// <summary>风控规则实体转响应 DTO</summary>
    /// <param name="rule">规则实体</param>
    /// <returns>规则响应</returns>
    public static RiskRuleResponse ToRuleResponse(RiskRule rule) => new()
    {
        Id = rule.Id,
        Name = rule.Name,
        Scene = rule.Scene,
        Dimension = rule.Dimension,
        WindowSeconds = rule.WindowSeconds,
        Threshold = rule.Threshold,
        Disposition = rule.Disposition,
        MerchantId = rule.MerchantId,
        Description = rule.Description,
        Enabled = rule.Enabled,
        CreatedAt = rule.CreatedAt,
        UpdatedAt = rule.UpdatedAt,
    };

    /// <summary>风险案例实体转响应 DTO</summary>
    /// <param name="riskCase">案例实体</param>
    /// <returns>案例响应</returns>
    public static RiskCaseResponse ToCaseResponse(RiskCase riskCase) => new()
    {
        Id = riskCase.Id,
        RuleId = riskCase.RuleId,
        RuleName = riskCase.RuleName,
        Scene = riskCase.Scene,
        Dimension = riskCase.Dimension,
        DimensionKey = riskCase.DimensionKey,
        UserId = riskCase.UserId,
        MerchantId = riskCase.MerchantId,
        Ip = riskCase.Ip,
        DeviceId = riskCase.DeviceId,
        OccurredCount = riskCase.OccurredCount,
        Threshold = riskCase.Threshold,
        Disposition = riskCase.Disposition,
        Source = riskCase.Source,
        Summary = riskCase.Summary,
        Status = riskCase.Status,
        ResolutionNote = riskCase.ResolutionNote,
        ResolvedAt = riskCase.ResolvedAt,
        CreatedAt = riskCase.CreatedAt,
    };

    /// <summary>黑名单实体转响应 DTO</summary>
    /// <param name="entry">黑名单实体</param>
    /// <param name="now">当前时间（UTC，判断是否过期）</param>
    /// <returns>黑名单响应</returns>
    public static BlacklistResponse ToBlacklistResponse(BlacklistEntry entry, DateTime now) => new()
    {
        Id = entry.Id,
        TargetType = entry.TargetType,
        TargetValue = entry.TargetValue,
        Reason = entry.Reason,
        ExpiresAt = entry.ExpiresAt,
        MerchantId = entry.MerchantId,
        Enabled = entry.Enabled,
        Expired = entry.IsExpired(now),
        CreatedAt = entry.CreatedAt,
    };

    /// <summary>风控事件实体转响应 DTO</summary>
    /// <param name="riskEvent">事件实体</param>
    /// <returns>事件响应</returns>
    public static RiskEventResponse ToEventResponse(RiskEvent riskEvent) => new()
    {
        Id = riskEvent.Id,
        Scene = riskEvent.Scene,
        UserId = riskEvent.UserId,
        MerchantId = riskEvent.MerchantId,
        Ip = riskEvent.Ip,
        DeviceId = riskEvent.DeviceId,
        PayloadJson = riskEvent.PayloadJson,
        OccurredAt = riskEvent.OccurredAt,
    };
}

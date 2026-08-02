using BuildingBlocks.Core.Entities;

namespace RiskService.Domain.Entities;

/// <summary>
/// 风控事件 — 各业务服务上报的行为事件（下单/领券/登录失败/评价等），规则引擎按窗口聚合评估。
/// 事件流水只追加不改（审计日志性质），保留期由配置 EventRetentionDays 控制。
/// </summary>
public sealed class RiskEvent : Entity
{
    private RiskEvent() { } // EF Core

    /// <summary>创建风控事件</summary>
    /// <param name="scene">场景编码（与规则场景匹配）</param>
    /// <param name="userId">用户 ID（可选）</param>
    /// <param name="merchantId">商户 ID（可选）</param>
    /// <param name="ip">客户端 IP（可选）</param>
    /// <param name="deviceId">设备 ID（可选）</param>
    /// <param name="payloadJson">附加载荷 JSON（可选，如订单号/商品ID）</param>
    /// <param name="occurredAt">事件发生时间（UTC）</param>
    public RiskEvent(string scene, Guid? userId, Guid? merchantId, string? ip, string? deviceId,
        string? payloadJson = null, DateTime? occurredAt = null)
    {
        Scene = (scene ?? string.Empty).Trim().ToUpperInvariant();
        UserId = userId;
        MerchantId = merchantId;
        Ip = ip?.Trim();
        DeviceId = deviceId?.Trim();
        PayloadJson = payloadJson;
        OccurredAt = occurredAt ?? DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>场景编码（ORDER_SUBMIT / COUPON_CLAIM / LOGIN_FAIL / REVIEW_SUBMIT）</summary>
    public string Scene { get; private set; } = string.Empty;

    /// <summary>用户 ID（可选）</summary>
    public Guid? UserId { get; private set; }

    /// <summary>商户 ID（可选）</summary>
    public Guid? MerchantId { get; private set; }

    /// <summary>客户端 IP（可选）</summary>
    public string? Ip { get; private set; }

    /// <summary>设备 ID（可选）</summary>
    public string? DeviceId { get; private set; }

    /// <summary>附加载荷 JSON（可选）</summary>
    public string? PayloadJson { get; private set; }

    /// <summary>事件发生时间（UTC）</summary>
    public DateTime OccurredAt { get; private set; }
}

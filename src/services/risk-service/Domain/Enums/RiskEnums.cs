namespace RiskService.Domain.Enums;

/// <summary>
/// 风控维度 — 规则按哪个维度聚合统计事件。
/// </summary>
public enum RiskDimension
{
    /// <summary>按用户（UserId）</summary>
    User = 0,

    /// <summary>按 IP（客户端 IP）</summary>
    Ip = 1,

    /// <summary>按设备（DeviceId）</summary>
    Device = 2,

    /// <summary>按商户（MerchantId）</summary>
    Merchant = 3,
}

/// <summary>
/// 风控处置级别 — 命中规则后的处置方式。
/// </summary>
public enum RiskDisposition
{
    /// <summary>观察（Watch）：仅记录案例，不拦截业务</summary>
    Watch = 0,

    /// <summary>拦截（Block）：业务方决策接口返回拦截，阻止后续操作</summary>
    Block = 1,
}

/// <summary>
/// 风险案例状态机：Open（待处置）→ Reviewing（人工复核中）→ Resolved（确认风险）/ FalsePositive（误报）。
/// </summary>
public enum RiskCaseStatus
{
    /// <summary>待处置（规则命中自动生成）</summary>
    Open = 0,

    /// <summary>人工复核中</summary>
    Reviewing = 1,

    /// <summary>确认风险（已处置）</summary>
    Resolved = 2,

    /// <summary>误报（已处置）</summary>
    FalsePositive = 3,
}

/// <summary>
/// 黑名单对象类型。
/// </summary>
public enum BlacklistTargetType
{
    /// <summary>用户</summary>
    User = 0,

    /// <summary>IP 地址</summary>
    Ip = 1,

    /// <summary>设备 ID</summary>
    Device = 2,
}

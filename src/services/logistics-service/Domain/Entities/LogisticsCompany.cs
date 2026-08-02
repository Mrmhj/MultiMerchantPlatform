using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;

namespace LogisticsService.Domain.Entities;

/// <summary>
/// 物流公司 — 平台级基础数据（非多租户），供商户发货时选择。
/// 演示环境无真实物流 API，轨迹推进通过内部接口模拟物流公司回调。
/// </summary>
public sealed class LogisticsCompany : Entity
{
    private LogisticsCompany() { } // EF Core

    /// <summary>创建物流公司（默认启用）</summary>
    /// <param name="code">编码（唯一，如 SF/YTO/ZTO）</param>
    /// <param name="name">名称（如 顺丰速运）</param>
    /// <param name="trackingUrlTemplate">查询链接模板（可选，{no} 占位运单号）</param>
    public LogisticsCompany(string code, string name, string? trackingUrlTemplate = null)
    {
        ChangeCode(code);
        ChangeName(name);
        TrackingUrlTemplate = string.IsNullOrWhiteSpace(trackingUrlTemplate) ? null : trackingUrlTemplate.Trim();
        IsEnabled = true;
    }

    /// <summary>编码（唯一）</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>名称</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>查询链接模板（{no} 替换为运单号）</summary>
    public string? TrackingUrlTemplate { get; private set; }

    /// <summary>是否启用（停用后商户不可选）</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>修改编码</summary>
    /// <param name="code">编码（2-20 字符）</param>
    public void ChangeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length is < 2 or > 20)
            throw new DomainException("物流公司编码需在 2-20 字符之间", "INVALID_COMPANY_CODE");
        Code = code.Trim().ToUpperInvariant();
    }

    /// <summary>修改名称</summary>
    /// <param name="name">名称（1-50 字符）</param>
    public void ChangeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 50)
            throw new DomainException("物流公司名称需在 1-50 字符之间", "INVALID_COMPANY_NAME");
        Name = name.Trim();
    }

    /// <summary>修改查询链接模板</summary>
    /// <param name="trackingUrlTemplate">链接模板（可选）</param>
    public void ChangeTrackingUrl(string? trackingUrlTemplate)
        => TrackingUrlTemplate = string.IsNullOrWhiteSpace(trackingUrlTemplate) ? null : trackingUrlTemplate.Trim();

    /// <summary>启用</summary>
    public void Enable() => IsEnabled = true;

    /// <summary>停用</summary>
    public void Disable() => IsEnabled = false;
}

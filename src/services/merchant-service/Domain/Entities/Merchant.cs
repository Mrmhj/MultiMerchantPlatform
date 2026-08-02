using BuildingBlocks.Core.Entities;
using MerchantService.Domain.Enums;

namespace MerchantService.Domain.Entities;

/// <summary>
/// 商户实体 — 平台商户（入驻主体）。
/// 状态机（Pending/Approved/Rejected/Disabled）与审核行为内聚在实体方法（充血模型）。
/// </summary>
public sealed class Merchant : Entity
{
    private Merchant() { } // EF Core

    /// <summary>创建入驻申请（初始状态 Pending）</summary>
    /// <param name="ownerUserId">申请人的用户 ID（identity-service 用户）</param>
    /// <param name="name">商户名称</param>
    /// <param name="licenseNo">营业执照号</param>
    /// <param name="contactName">联系人姓名</param>
    /// <param name="contactPhone">联系人电话</param>
    /// <param name="contactEmail">联系邮箱（可选）</param>
    /// <param name="description">商户简介（可选）</param>
    public Merchant(
        Guid ownerUserId,
        string name,
        string licenseNo,
        string contactName,
        string contactPhone,
        string? contactEmail = null,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseNo);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactPhone);

        OwnerUserId = ownerUserId;
        Name = name.Trim();
        LicenseNo = licenseNo.Trim();
        ContactName = contactName.Trim();
        ContactPhone = contactPhone.Trim();
        ContactEmail = contactEmail?.Trim();
        Description = description?.Trim();
        Status = MerchantStatus.Pending;
    }

    /// <summary>申请人用户 ID</summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>商户名称</summary>
    public string Name { get; private set; } = null!;

    /// <summary>营业执照号</summary>
    public string LicenseNo { get; private set; } = null!;

    /// <summary>联系人姓名</summary>
    public string ContactName { get; private set; } = null!;

    /// <summary>联系人电话</summary>
    public string ContactPhone { get; private set; } = null!;

    /// <summary>联系邮箱（可选）</summary>
    public string? ContactEmail { get; private set; }

    /// <summary>商户简介（可选）</summary>
    public string? Description { get; private set; }

    /// <summary>审核状态（Pending/Approved/Rejected/Disabled）</summary>
    public MerchantStatus Status { get; private set; }

    /// <summary>驳回原因（Rejected 时）</summary>
    public string? RejectReason { get; private set; }

    /// <summary>审核通过时间</summary>
    public DateTime? ApprovedAt { get; private set; }

    /// <summary>审核通过 — 状态转 Approved，记录通过时间</summary>
    /// <param name="timeProvider">时间提供器</param>
    public void Approve(TimeProvider timeProvider)
    {
        Status = MerchantStatus.Approved;
        ApprovedAt = timeProvider.GetUtcNow().UtcDateTime;
        RejectReason = null;
    }

    /// <summary>审核驳回 — 状态转 Rejected，记录驳回原因</summary>
    /// <param name="reason">驳回原因（必填）</param>
    public void Reject(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Status = MerchantStatus.Rejected;
        RejectReason = reason.Trim();
        ApprovedAt = null;
    }

    /// <summary>禁用商户（平台处罚，不可营业）</summary>
    public void Disable()
    {
        if (Status == MerchantStatus.Approved)
            Status = MerchantStatus.Disabled;
    }

    /// <summary>启用商户（恢复营业）</summary>
    public void Enable()
    {
        if (Status == MerchantStatus.Disabled)
            Status = MerchantStatus.Approved;
    }

    /// <summary>更新商户资料（联系信息 / 简介）</summary>
    /// <param name="contactName">联系人姓名</param>
    /// <param name="contactPhone">联系人电话</param>
    /// <param name="contactEmail">联系邮箱</param>
    /// <param name="description">简介</param>
    public void UpdateProfile(string contactName, string contactPhone, string? contactEmail = null, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contactName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactPhone);

        ContactName = contactName.Trim();
        ContactPhone = contactPhone.Trim();
        ContactEmail = contactEmail?.Trim();
        Description = description?.Trim();
    }
}

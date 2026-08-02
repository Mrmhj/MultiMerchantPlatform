using System.ComponentModel.DataAnnotations;

namespace MerchantService.DTOs;

/// <summary>商户入驻申请请求</summary>
public sealed record MerchantApplyRequest
{
    /// <summary>商户名称（唯一）</summary>
    [Required, StringLength(100, MinimumLength = 2)]
    public required string Name { get; init; }

    /// <summary>营业执照号</summary>
    [Required, StringLength(50, MinimumLength = 6)]
    public required string LicenseNo { get; init; }

    /// <summary>联系人姓名</summary>
    [Required, StringLength(50, MinimumLength = 1)]
    public required string ContactName { get; init; }

    /// <summary>联系人电话</summary>
    [Required, StringLength(20, MinimumLength = 5)]
    public required string ContactPhone { get; init; }

    /// <summary>联系邮箱（可选）</summary>
    [EmailAddress, StringLength(200)]
    public string? ContactEmail { get; init; }

    /// <summary>商户简介（可选）</summary>
    [StringLength(1000)]
    public string? Description { get; init; }
}

/// <summary>商户审核请求</summary>
public sealed record MerchantReviewRequest
{
    /// <summary>是否通过（true 通过 / false 驳回）</summary>
    [Required]
    public bool Approved { get; init; }

    /// <summary>驳回原因（Approved=false 时必填）</summary>
    [StringLength(500)]
    public string? Reason { get; init; }
}

/// <summary>商户响应</summary>
public sealed record MerchantResponse
{
    /// <summary>商户 ID</summary>
    public Guid Id { get; init; }

    /// <summary>申请人用户 ID</summary>
    public Guid OwnerUserId { get; init; }

    /// <summary>商户名称</summary>
    public required string Name { get; init; }

    /// <summary>营业执照号</summary>
    public required string LicenseNo { get; init; }

    /// <summary>联系人姓名</summary>
    public required string ContactName { get; init; }

    /// <summary>联系人电话</summary>
    public required string ContactPhone { get; init; }

    /// <summary>联系邮箱</summary>
    public string? ContactEmail { get; init; }

    /// <summary>商户简介</summary>
    public string? Description { get; init; }

    /// <summary>审核状态（1=待审核 2=已通过 3=已驳回 4=已禁用）</summary>
    public int Status { get; init; }

    /// <summary>驳回原因</summary>
    public string? RejectReason { get; init; }

    /// <summary>审核通过时间</summary>
    public DateTime? ApprovedAt { get; init; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; init; }
}

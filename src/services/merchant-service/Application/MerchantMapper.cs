using MerchantService.Domain.Entities;
using MerchantService.DTOs;

namespace MerchantService.Application;

/// <summary>
/// 实体 → DTO 映射。
/// </summary>
public static class MerchantMapper
{
    /// <summary>商户实体转响应 DTO</summary>
    /// <param name="merchant">商户实体</param>
    /// <returns>商户信息响应</returns>
    public static MerchantResponse ToResponse(Merchant merchant) => new()
    {
        Id = merchant.Id,
        OwnerUserId = merchant.OwnerUserId,
        Name = merchant.Name,
        LicenseNo = merchant.LicenseNo,
        ContactName = merchant.ContactName,
        ContactPhone = merchant.ContactPhone,
        ContactEmail = merchant.ContactEmail,
        Description = merchant.Description,
        Status = (int)merchant.Status,
        RejectReason = merchant.RejectReason,
        ApprovedAt = merchant.ApprovedAt,
        CreatedAt = merchant.CreatedAt,
    };
}

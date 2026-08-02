using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.Security;
using MerchantService.Domain.Entities;
using MerchantService.DTOs;
using MerchantService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MerchantService.Application.Commands;

/// <summary>入驻申请命令 — 当前登录用户提交商户入驻申请（状态 Pending）</summary>
public sealed record ApplyMerchantCommand(
    string Name,
    string LicenseNo,
    string ContactName,
    string ContactPhone,
    string? ContactEmail,
    string? Description) : ICommand<MerchantResponse>;

/// <summary>入驻申请命令处理器</summary>
public sealed class ApplyMerchantCommandHandler(
    MerchantDbContext db,
    ICurrentUser currentUser) : ICommandHandler<ApplyMerchantCommand, MerchantResponse>
{
    /// <inheritdoc />
    public async Task<MerchantResponse> HandleAsync(ApplyMerchantCommand command, CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId == Guid.Empty)
            throw new DomainException("请先登录再提交入驻申请", "UNAUTHENTICATED");

        // 商户名唯一
        var nameExists = await db.Merchants.AnyAsync(m => m.Name == command.Name.Trim(), ct);
        if (nameExists)
            throw new DomainException("商户名称已被占用", "NAME_EXISTS");

        // 一个用户只能有一条未终态的申请（Pending 或已通过）
        var hasActive = await db.Merchants.AnyAsync(
            m => m.OwnerUserId == currentUser.UserId
                && m.Status != MerchantService.Domain.Enums.MerchantStatus.Rejected
                && m.Status != MerchantService.Domain.Enums.MerchantStatus.Disabled,
            ct);
        if (hasActive)
            throw new DomainException("您已有待审核或已通过的商户申请", "DUPLICATE_APPLICATION");

        var merchant = new Merchant(
            currentUser.UserId,
            command.Name,
            command.LicenseNo,
            command.ContactName,
            command.ContactPhone,
            command.ContactEmail,
            command.Description);

        db.Merchants.Add(merchant);
        await db.SaveChangesAsync(ct);

        return MerchantMapper.ToResponse(merchant);
    }
}

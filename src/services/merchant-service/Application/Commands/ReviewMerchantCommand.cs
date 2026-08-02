using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using MerchantService.Domain.Enums;
using MerchantService.DTOs;
using MerchantService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MerchantService.Application.Commands;

/// <summary>商户审核命令 — 管理员批准/驳回入驻申请（仅 admin 角色）</summary>
public sealed record ReviewMerchantCommand(
    Guid MerchantId,
    bool Approved,
    string? Reason) : ICommand<MerchantResponse>;

/// <summary>商户审核命令处理器</summary>
public sealed class ReviewMerchantCommandHandler(
    MerchantDbContext db,
    TimeProvider timeProvider) : ICommandHandler<ReviewMerchantCommand, MerchantResponse>
{
    /// <inheritdoc />
    public async Task<MerchantResponse> HandleAsync(ReviewMerchantCommand command, CancellationToken ct = default)
    {
        var merchant = await db.Merchants.FirstOrDefaultAsync(m => m.Id == command.MerchantId, ct);
        if (merchant is null)
            throw new NotFoundException("商户", command.MerchantId);

        if (merchant.Status != MerchantStatus.Pending)
            throw new DomainException($"商户当前状态不允许审核（当前：{merchant.Status}）", "INVALID_STATE");

        if (command.Approved)
        {
            merchant.Approve(timeProvider);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(command.Reason))
                throw new DomainException("驳回时必须填写原因", "REASON_REQUIRED");
            merchant.Reject(command.Reason);
        }

        await db.SaveChangesAsync(ct);
        return MerchantMapper.ToResponse(merchant);
    }
}

using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.MultiTenant;
using PromotionService.Domain.Entities;
using PromotionService.DTOs;
using PromotionService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PromotionService.Application.Commands;

/// <summary>创建满减活动命令（商户端）</summary>
/// <param name="Request">活动信息</param>
public sealed record CreateActivityCommand(CreateActivityRequest Request) : ICommand<ActivityResponse>;

/// <summary>创建满减活动命令处理器</summary>
public sealed class CreateActivityCommandHandler(
    PromotionDbContext db,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider) : ICommandHandler<CreateActivityCommand, ActivityResponse>
{
    /// <inheritdoc />
    public async Task<ActivityResponse> HandleAsync(CreateActivityCommand command, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var r = command.Request;
        var activity = new PromotionActivity(merchantId, r.Name, r.ThresholdAmount, r.DiscountAmount,
            r.StartTime, r.EndTime);
        db.Activities.Add(activity);
        await db.SaveChangesAsync(ct);

        return PromotionMapper.ToActivityResponse(activity, timeProvider.GetUtcNow().UtcDateTime);
    }
}

/// <summary>变更满减活动状态命令（启用/停用）</summary>
/// <param name="Id">活动 ID</param>
/// <param name="Active">目标状态：true 启用 / false 停用</param>
public sealed record ChangeActivityStatusCommand(Guid Id, bool Active) : ICommand<ActivityResponse>;

/// <summary>变更满减活动状态命令处理器</summary>
public sealed class ChangeActivityStatusCommandHandler(
    PromotionDbContext db,
    ITenantProvider tenantProvider,
    TimeProvider timeProvider) : ICommandHandler<ChangeActivityStatusCommand, ActivityResponse>
{
    /// <inheritdoc />
    public async Task<ActivityResponse> HandleAsync(ChangeActivityStatusCommand command, CancellationToken ct = default)
    {
        var merchantId = tenantProvider.CurrentMerchantId
            ?? throw new DomainException("缺少商户上下文（请求头 X-Merchant-Id）", "MERCHANT_REQUIRED");

        var activity = await db.Activities.FirstOrDefaultAsync(
            a => a.Id == command.Id && a.MerchantId == merchantId, ct)
            ?? throw new NotFoundException("满减活动", command.Id);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (command.Active)
            activity.Activate(now);
        else
            activity.Disable();

        await db.SaveChangesAsync(ct);
        return PromotionMapper.ToActivityResponse(activity, now);
    }
}

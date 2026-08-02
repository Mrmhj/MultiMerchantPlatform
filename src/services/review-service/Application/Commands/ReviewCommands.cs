using BuildingBlocks.Core.CQRS;
using BuildingBlocks.Core.Exceptions;
using BuildingBlocks.MultiTenant;
using ReviewService.Domain.Entities;
using ReviewService.DTOs;
using ReviewService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ReviewService.Application.Commands;

/// <summary>创建评价命令（买家端）</summary>
/// <param name="UserId">买家用户 ID</param>
/// <param name="Request">评价信息</param>
public sealed record CreateReviewCommand(Guid UserId, CreateReviewRequest Request) : ICommand<ReviewResponse>;

/// <summary>创建评价命令处理器（同一子订单项仅允许一条评价）</summary>
public sealed class CreateReviewCommandHandler(
    ReviewDbContext db) : ICommandHandler<CreateReviewCommand, ReviewResponse>
{
    /// <inheritdoc />
    public async Task<ReviewResponse> HandleAsync(CreateReviewCommand command, CancellationToken ct = default)
    {
        var r = command.Request;

        // 防重复评价：同一用户对同一子订单项（商品）只能评一次
        var exists = await db.Reviews.AnyAsync(
            x => x.UserId == command.UserId && x.SubOrderId == r.SubOrderId && x.ProductId == r.ProductId, ct);
        if (exists)
            throw new DomainException("该订单商品已评价，不能重复评价", "REVIEW_ALREADY_EXISTS");

        var review = new Review(command.UserId, r.MerchantId, r.OrderId, r.SubOrderId,
            r.ProductId, r.ProductName, r.SkuId, r.SkuSpec ?? string.Empty,
            r.Rating, r.Content, r.IsAnonymous);
        db.Reviews.Add(review);
        await db.SaveChangesAsync(ct);

        return ReviewMapper.ToResponse(review);
    }
}

/// <summary>商户回复评价命令</summary>
/// <param name="MerchantId">商户 ID（X-Merchant-Id）</param>
/// <param name="ReviewId">评价 ID</param>
/// <param name="Reply">回复内容</param>
public sealed record ReplyReviewCommand(Guid MerchantId, Guid ReviewId, string Reply) : ICommand<ReviewResponse>;

/// <summary>商户回复评价命令处理器（可修改回复，重复回复覆盖）</summary>
public sealed class ReplyReviewCommandHandler(
    ReviewDbContext db,
    TimeProvider timeProvider) : ICommandHandler<ReplyReviewCommand, ReviewResponse>
{
    /// <inheritdoc />
    public async Task<ReviewResponse> HandleAsync(ReplyReviewCommand command, CancellationToken ct = default)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(
            x => x.Id == command.ReviewId && x.MerchantId == command.MerchantId, ct)
            ?? throw new NotFoundException("评价", command.ReviewId);

        review.Reply(command.Reply, timeProvider.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return ReviewMapper.ToResponse(review);
    }
}

/// <summary>商户变更评价状态命令（隐藏/恢复可见）</summary>
/// <param name="MerchantId">商户 ID（X-Merchant-Id）</param>
/// <param name="ReviewId">评价 ID</param>
/// <param name="Visible">目标状态：true 可见 / false 隐藏</param>
public sealed record ChangeReviewStatusCommand(Guid MerchantId, Guid ReviewId, bool Visible) : ICommand<ReviewResponse>;

/// <summary>商户变更评价状态命令处理器</summary>
public sealed class ChangeReviewStatusCommandHandler(
    ReviewDbContext db) : ICommandHandler<ChangeReviewStatusCommand, ReviewResponse>
{
    /// <inheritdoc />
    public async Task<ReviewResponse> HandleAsync(ChangeReviewStatusCommand command, CancellationToken ct = default)
    {
        var review = await db.Reviews.FirstOrDefaultAsync(
            x => x.Id == command.ReviewId && x.MerchantId == command.MerchantId, ct)
            ?? throw new NotFoundException("评价", command.ReviewId);

        if (command.Visible)
            review.Show();
        else
            review.Hide();

        await db.SaveChangesAsync(ct);
        return ReviewMapper.ToResponse(review);
    }
}

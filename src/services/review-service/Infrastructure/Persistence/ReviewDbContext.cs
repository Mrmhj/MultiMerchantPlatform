using BuildingBlocks.Core.Entities;
using BuildingBlocks.MultiTenant;
using Microsoft.EntityFrameworkCore;
using ReviewService.Domain.Entities;

namespace ReviewService.Infrastructure.Persistence;

/// <summary>
/// 评价数据库上下文（MMP_Review 库）。
/// 多租户隔离：全局查询过滤器按当前商户 ID（HasQueryFilter），平台 admin（无商户上下文）可读全量。
/// 买家维度（UserId）由 Handler 显式过滤，买家仅可操作自己的评价。
/// </summary>
public sealed class ReviewDbContext(
    DbContextOptions<ReviewDbContext> options,
    ITenantProvider tenantProvider) : DbContext(options)
{
    private readonly ITenantProvider _tenantProvider = tenantProvider;

    /// <summary>评价表</summary>
    public DbSet<Review> Reviews => Set<Review>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Review>(e =>
        {
            e.ToTable("Reviews");
            e.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            e.Property(x => x.SkuSpec).HasMaxLength(100);
            e.Property(x => x.Content).HasMaxLength(500).IsRequired();
            e.Property(x => x.ReplyContent).HasMaxLength(500);
            // 同一用户对同一子订单项（商品）仅一条评价
            e.HasIndex(x => new { x.UserId, x.SubOrderId, x.ProductId }).IsUnique();
            e.HasIndex(x => new { x.MerchantId, x.CreatedAt });
            e.HasIndex(x => new { x.ProductId, x.Status, x.CreatedAt });
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            // 多租户隔离：当前商户只可见自己商品的评价
            e.HasQueryFilter(r => _tenantProvider.CurrentMerchantId == null || r.MerchantId == _tenantProvider.CurrentMerchantId);
        });
    }
}

using BuildingBlocks.Core.Entities;
using BuildingBlocks.MultiTenant;
using Microsoft.EntityFrameworkCore;
using PromotionService.Domain.Entities;

namespace PromotionService.Infrastructure.Persistence;

/// <summary>
/// 促销数据库上下文（MMP_Promotion 库）。
/// 多租户隔离：全局查询过滤器按当前商户 ID（HasQueryFilter），平台 admin（无商户上下文）可读全量。
/// 注意：UserCoupon 为买家维度（UserId 隔离），不受商户过滤器约束。
/// </summary>
public sealed class PromotionDbContext(
    DbContextOptions<PromotionDbContext> options,
    ITenantProvider tenantProvider) : DbContext(options)
{
    private readonly ITenantProvider _tenantProvider = tenantProvider;

    /// <summary>优惠券模板表</summary>
    public DbSet<Coupon> Coupons => Set<Coupon>();

    /// <summary>用户优惠券表（买家维度）</summary>
    public DbSet<UserCoupon> UserCoupons => Set<UserCoupon>();

    /// <summary>满减活动表</summary>
    public DbSet<PromotionActivity> Activities => Set<PromotionActivity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Coupon>(e =>
        {
            e.ToTable("Coupons");
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.Property(x => x.ThresholdAmount).HasPrecision(18, 2);
            e.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            e.HasIndex(x => new { x.MerchantId, x.Status });
            e.HasIndex(x => new { x.MerchantId, x.CreatedAt });

            // 多租户隔离：当前商户只可见自己的优惠券模板
            e.HasQueryFilter(c => _tenantProvider.CurrentMerchantId == null || c.MerchantId == _tenantProvider.CurrentMerchantId);
        });

        modelBuilder.Entity<UserCoupon>(e =>
        {
            e.ToTable("UserCoupons");
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.Property(x => x.ThresholdAmount).HasPrecision(18, 2);
            e.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            e.HasIndex(x => new { x.UserId, x.Status });
            e.HasIndex(x => new { x.UserId, x.CouponId });
            e.HasIndex(x => x.MerchantId);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<PromotionActivity>(e =>
        {
            e.ToTable("PromotionActivities");
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.ThresholdAmount).HasPrecision(18, 2);
            e.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            e.HasIndex(x => new { x.MerchantId, x.Status });
            e.HasIndex(x => new { x.MerchantId, x.CreatedAt });

            // 多租户隔离
            e.HasQueryFilter(a => _tenantProvider.CurrentMerchantId == null || a.MerchantId == _tenantProvider.CurrentMerchantId);
        });
    }
}

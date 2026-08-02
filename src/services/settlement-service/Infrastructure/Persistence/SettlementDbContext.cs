using BuildingBlocks.MultiTenant;
using SettlementService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace SettlementService.Infrastructure.Persistence;

/// <summary>
/// 结算数据库上下文（MMP_Settlement 库）。
/// 多租户隔离：结算单 / 佣金规则按商户（HasQueryFilter），平台 admin / 内部接口（无商户上下文）可读全量。
/// </summary>
public sealed class SettlementDbContext(
    DbContextOptions<SettlementDbContext> options,
    ITenantProvider tenantProvider) : DbContext(options)
{
    private readonly ITenantProvider _tenantProvider = tenantProvider;

    /// <summary>结算单表</summary>
    public DbSet<Settlement> Settlements => Set<Settlement>();

    /// <summary>结算明细表</summary>
    public DbSet<SettlementItem> SettlementItems => Set<SettlementItem>();

    /// <summary>佣金规则表</summary>
    public DbSet<CommissionRule> CommissionRules => Set<CommissionRule>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Settlement>(e =>
        {
            e.ToTable("Settlements");
            e.Property(x => x.MerchantName).HasMaxLength(100).IsRequired();
            e.Property(x => x.TotalOrderAmount).HasPrecision(18, 2);
            e.Property(x => x.TotalCommission).HasPrecision(18, 2);
            e.HasIndex(x => new { x.MerchantId, x.CreatedAt });
            e.HasIndex(x => new { x.MerchantId, x.CycleStart, x.CycleEnd });
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            // 多租户隔离：当前商户只可见自己的结算单
            e.HasQueryFilter(s => _tenantProvider.CurrentMerchantId == null || s.MerchantId == _tenantProvider.CurrentMerchantId);
        });

        modelBuilder.Entity<SettlementItem>(e =>
        {
            e.ToTable("SettlementItems");
            e.Property(x => x.OrderNo).HasMaxLength(40).IsRequired();
            e.Property(x => x.ProductAmount).HasPrecision(18, 2);
            e.Property(x => x.CommissionAmount).HasPrecision(18, 2);
            // 一个子订单仅结算一次
            e.HasIndex(x => x.SubOrderId).IsUnique();
            e.HasIndex(x => x.SettlementId);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<CommissionRule>(e =>
        {
            e.ToTable("CommissionRules");
            e.Property(x => x.Rate).HasPrecision(5, 2);
            // 一个商户一条规则
            e.HasIndex(x => x.MerchantId).IsUnique();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });
    }
}

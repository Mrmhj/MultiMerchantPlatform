using BuildingBlocks.MultiTenant;
using LogisticsService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LogisticsService.Infrastructure.Persistence;

/// <summary>
/// 物流数据库上下文（MMP_Logistics 库）。
/// 多租户隔离：运单按商户（HasQueryFilter），平台 admin / 内部接口（无商户上下文）可读全量。
/// 买家维度（BuyerUserId）由 Handler 显式过滤。
/// </summary>
public sealed class LogisticsDbContext(
    DbContextOptions<LogisticsDbContext> options,
    ITenantProvider tenantProvider) : DbContext(options)
{
    private readonly ITenantProvider _tenantProvider = tenantProvider;

    /// <summary>物流公司表</summary>
    public DbSet<LogisticsCompany> Companies => Set<LogisticsCompany>();

    /// <summary>运单表</summary>
    public DbSet<Shipment> Shipments => Set<Shipment>();

    /// <summary>运单轨迹表</summary>
    public DbSet<ShipmentTrack> Tracks => Set<ShipmentTrack>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LogisticsCompany>(e =>
        {
            e.ToTable("LogisticsCompanies");
            e.Property(x => x.Code).HasMaxLength(20).IsRequired();
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.Property(x => x.TrackingUrlTemplate).HasMaxLength(300);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<Shipment>(e =>
        {
            e.ToTable("Shipments");
            e.Property(x => x.OrderNo).HasMaxLength(40).IsRequired();
            e.Property(x => x.CarrierCode).HasMaxLength(50).IsRequired();
            e.Property(x => x.CarrierName).HasMaxLength(50).IsRequired();
            e.Property(x => x.TrackingNo).HasMaxLength(64).IsRequired();
            // 一个子订单仅一条运单
            e.HasIndex(x => x.SubOrderId).IsUnique();
            e.HasIndex(x => new { x.MerchantId, x.CreatedAt });
            e.HasIndex(x => x.TrackingNo).IsUnique();
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            // 多租户隔离：当前商户只可见自己的运单
            e.HasQueryFilter(s => _tenantProvider.CurrentMerchantId == null || s.MerchantId == _tenantProvider.CurrentMerchantId);
        });

        modelBuilder.Entity<ShipmentTrack>(e =>
        {
            e.ToTable("ShipmentTracks");
            e.Property(x => x.Description).HasMaxLength(200).IsRequired();
            e.Property(x => x.Location).HasMaxLength(100);
            e.HasIndex(x => new { x.ShipmentId, x.TrackedAt });
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });
    }
}

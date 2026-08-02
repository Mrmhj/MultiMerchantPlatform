using BiAdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BiAdminService.Infrastructure.Persistence;

/// <summary>
/// BI 分析数据库上下文（MMP_BI 库）— 聚合表（同步时重建，非业务明细）。
/// </summary>
public sealed class BiDbContext(DbContextOptions<BiDbContext> options) : DbContext(options)
{
    /// <summary>总览快照表</summary>
    public DbSet<BiOverview> Overviews => Set<BiOverview>();

    /// <summary>按天销售聚合表</summary>
    public DbSet<BiDailySales> DailySales => Set<BiDailySales>();

    /// <summary>商户销售排行聚合表</summary>
    public DbSet<BiMerchantSales> MerchantSales => Set<BiMerchantSales>();

    /// <summary>商品销售排行聚合表</summary>
    public DbSet<BiProductSales> ProductSales => Set<BiProductSales>();

    /// <summary>主订单状态分布表</summary>
    public DbSet<BiOrderStatusDist> OrderStatusDist => Set<BiOrderStatusDist>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BiOverview>(e =>
        {
            e.ToTable("BiOverviews");
            e.Property(x => x.TotalGmv).HasPrecision(18, 2);
        });

        modelBuilder.Entity<BiDailySales>(e =>
        {
            e.ToTable("BiDailySales");
            e.Property(x => x.Gmv).HasPrecision(18, 2);
            e.HasIndex(x => x.Date);
        });

        modelBuilder.Entity<BiMerchantSales>(e =>
        {
            e.ToTable("BiMerchantSales");
            e.Property(x => x.MerchantName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Gmv).HasPrecision(18, 2);
            e.HasIndex(x => new { x.MerchantId, x.Gmv });
        });

        modelBuilder.Entity<BiProductSales>(e =>
        {
            e.ToTable("BiProductSales");
            e.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasIndex(x => new { x.ProductId, x.Amount });
        });

        modelBuilder.Entity<BiOrderStatusDist>(e =>
        {
            e.ToTable("BiOrderStatusDist");
            e.HasIndex(x => x.Status);
        });
    }
}

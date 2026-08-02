using BuildingBlocks.Core.Entities;
using BuildingBlocks.MultiTenant;
using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Entities;

namespace ProductService.Infrastructure.Persistence;

/// <summary>
/// 商品数据库上下文（MMP_Product 库）。
/// 多租户隔离：全局查询过滤器按当前商户 ID（HasQueryFilter），平台 admin（无商户上下文）可读全量。
/// </summary>
public sealed class ProductDbContext(
    DbContextOptions<ProductDbContext> options,
    ITenantProvider tenantProvider) : DbContext(options)
{
    private readonly ITenantProvider _tenantProvider = tenantProvider;

    /// <summary>分类表</summary>
    public DbSet<Category> Categories => Set<Category>();

    /// <summary>商品表</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>商品 SKU 表</summary>
    public DbSet<ProductSku> ProductSkus => Set<ProductSku>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>(e =>
        {
            e.ToTable("Categories");
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(x => new { x.MerchantId, x.ParentId, x.Name }).IsUnique();
            e.HasIndex(x => new { x.MerchantId, x.SortOrder });

            // 多租户隔离：当前商户只可见自己的分类
            e.HasQueryFilter(c => _tenantProvider.CurrentMerchantId == null || c.MerchantId == _tenantProvider.CurrentMerchantId);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("Products");
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.CoverImage).HasMaxLength(500);
            e.HasIndex(x => new { x.MerchantId, x.CategoryId });
            e.HasIndex(x => new { x.MerchantId, x.Status });

            // 多租户隔离
            e.HasQueryFilter(p => _tenantProvider.CurrentMerchantId == null || p.MerchantId == _tenantProvider.CurrentMerchantId);
        });

        modelBuilder.Entity<ProductSku>(e =>
        {
            e.ToTable("ProductSkus");
            e.Property(x => x.SkuCode).HasMaxLength(50).IsRequired();
            e.Property(x => x.Spec).HasMaxLength(100).IsRequired();
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.HasIndex(x => new { x.ProductId, x.SkuCode }).IsUnique();
        });
    }
}

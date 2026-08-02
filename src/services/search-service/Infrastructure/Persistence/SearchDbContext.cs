using BuildingBlocks.Core.Entities;
using SearchService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace SearchService.Infrastructure.Persistence;

/// <summary>
/// 搜索数据库上下文（MMP_Search）。
/// </summary>
public sealed class SearchDbContext(DbContextOptions<SearchDbContext> options) : DbContext(options)
{
    /// <summary>商品搜索索引</summary>
    public DbSet<ProductSearchIndex> Products => Set<ProductSearchIndex>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var e = modelBuilder.Entity<ProductSearchIndex>();
        e.ToTable("ProductSearchIndexes");
        e.Property(x => x.MerchantName).HasMaxLength(100).IsRequired();
        e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        e.Property(x => x.Description).HasMaxLength(2000);
        e.Property(x => x.CategoryName).HasMaxLength(50).IsRequired();
        e.Property(x => x.CoverImage).HasMaxLength(500);
        e.Property(x => x.PriceMin).HasPrecision(18, 2);
        e.Property(x => x.PriceMax).HasPrecision(18, 2);
        // 商品 ID 唯一（upsert 依据）
        e.HasIndex(x => x.ProductId).IsUnique();
        // 查询索引：状态 + 分类 + 名称
        e.HasIndex(x => new { x.Status, x.CategoryId });
        e.HasIndex(x => x.Name);
        e.HasIndex(x => x.MerchantId);
        // 审计字段
        e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

using StockService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace StockService.Infrastructure.Persistence;

/// <summary>
/// 库存数据库上下文（MMP_Stock 库）。
/// </summary>
public sealed class StockDbContext(DbContextOptions<StockDbContext> options) : DbContext(options)
{
    /// <summary>库存条目表</summary>
    public DbSet<StockItem> StockItems => Set<StockItem>();

    /// <summary>库存流水表</summary>
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StockItem>(e =>
        {
            e.ToTable("StockItems");
            e.HasIndex(x => x.SkuId).IsUnique();
            e.HasIndex(x => x.MerchantId);
        });

        modelBuilder.Entity<StockTransaction>(e =>
        {
            e.ToTable("StockTransactions");
            e.Property(x => x.ReferenceId).HasMaxLength(64);
            e.HasIndex(x => new { x.SkuId, x.CreatedAt });
        });
    }
}

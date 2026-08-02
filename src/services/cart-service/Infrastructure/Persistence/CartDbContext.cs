using BuildingBlocks.Core.Entities;
using CartService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CartService.Infrastructure.Persistence;

/// <summary>
/// 购物车数据库上下文（MMP_Cart）。
/// </summary>
public sealed class CartDbContext(DbContextOptions<CartDbContext> options) : DbContext(options)
{
    /// <summary>购物车条目</summary>
    public DbSet<CartItem> CartItems => Set<CartItem>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var e = modelBuilder.Entity<CartItem>();
        e.ToTable("CartItems");
        e.Property(x => x.MerchantName).HasMaxLength(100).IsRequired();
        e.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
        e.Property(x => x.SkuCode).HasMaxLength(50).IsRequired();
        e.Property(x => x.Spec).HasMaxLength(100);
        e.Property(x => x.UnitPrice).HasPrecision(18, 2);
        // 买家 + SKU 唯一（同 SKU 合并数量）
        e.HasIndex(x => new { x.UserId, x.SkuId }).IsUnique();
        e.HasIndex(x => x.UserId);
        e.HasIndex(x => x.MerchantId);
        // 审计字段
        e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
    }
}

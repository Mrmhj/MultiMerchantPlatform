using OrderService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace OrderService.Infrastructure.Persistence;

/// <summary>
/// 订单数据库上下文（MMP_Order 库）。
/// </summary>
public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    /// <summary>主订单表</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary>子订单表（拆单结果）</summary>
    public DbSet<SubOrder> SubOrders => Set<SubOrder>();

    /// <summary>订单商品项表</summary>
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(e =>
        {
            e.ToTable("Orders");
            e.Property(x => x.OrderNo).HasMaxLength(32).IsRequired();
            e.Property(x => x.Remark).HasMaxLength(500);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.HasIndex(x => x.OrderNo).IsUnique();
            e.HasIndex(x => new { x.BuyerUserId, x.Status });
        });

        modelBuilder.Entity<SubOrder>(e =>
        {
            e.ToTable("SubOrders");
            e.Property(x => x.MerchantName).HasMaxLength(100).IsRequired();
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.HasIndex(x => new { x.MerchantId, x.Status });
            e.HasIndex(x => x.OrderId);
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.ToTable("OrderItems");
            e.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            e.Property(x => x.SkuCode).HasMaxLength(50).IsRequired();
            e.Property(x => x.Spec).HasMaxLength(100).IsRequired();
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.HasIndex(x => x.SubOrderId);
        });
    }
}

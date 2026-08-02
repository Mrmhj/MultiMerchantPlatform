using PayService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace PayService.Infrastructure.Persistence;

/// <summary>
/// 支付数据库上下文（MMP_Pay 库）。
/// </summary>
public sealed class PayDbContext(DbContextOptions<PayDbContext> options) : DbContext(options)
{
    /// <summary>支付单表</summary>
    public DbSet<Payment> Payments => Set<Payment>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Payment>(e =>
        {
            e.ToTable("Payments");
            e.Property(x => x.PayNo).HasMaxLength(32).IsRequired();
            e.Property(x => x.Channel).HasMaxLength(30).IsRequired();
            e.Property(x => x.FailReason).HasMaxLength(500);
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasIndex(x => x.PayNo).IsUnique();
            e.HasIndex(x => new { x.BuyerUserId, x.Status });
            e.HasIndex(x => x.OrderId);
        });
    }
}

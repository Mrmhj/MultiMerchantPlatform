using MerchantService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MerchantService.Infrastructure.Persistence;

/// <summary>
/// 商户数据库上下文（MMP_Merchant 库）。
/// </summary>
public sealed class MerchantDbContext(DbContextOptions<MerchantDbContext> options) : DbContext(options)
{
    /// <summary>商户表</summary>
    public DbSet<Merchant> Merchants => Set<Merchant>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Merchant>(e =>
        {
            e.ToTable("Merchants");
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.LicenseNo).HasMaxLength(50).IsRequired();
            e.Property(x => x.ContactName).HasMaxLength(50).IsRequired();
            e.Property(x => x.ContactPhone).HasMaxLength(20).IsRequired();
            e.Property(x => x.ContactEmail).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.RejectReason).HasMaxLength(500);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => new { x.OwnerUserId, x.Status });
            e.HasIndex(x => x.Status);
        });
    }
}

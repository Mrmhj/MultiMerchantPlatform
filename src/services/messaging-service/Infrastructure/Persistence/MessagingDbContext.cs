using MessagingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MessagingService.Infrastructure.Persistence;

/// <summary>
/// 消息队列数据库上下文（MMP_Infra 库）。
/// </summary>
public sealed class MessagingDbContext(DbContextOptions<MessagingDbContext> options) : DbContext(options)
{
    public DbSet<MessageOutbox> MessageOutboxes => Set<MessageOutbox>();

    public DbSet<MessageSubscription> Subscriptions => Set<MessageSubscription>();

    public DbSet<MessageIdempotency> IdempotencyRecords => Set<MessageIdempotency>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MessageOutbox>(e =>
        {
            e.ToTable("MessageOutbox");
            e.Property(x => x.EventName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Payload).IsRequired();
            e.Property(x => x.RoutingKey).HasMaxLength(200);
            e.Property(x => x.LastError).HasMaxLength(1000);
            e.HasIndex(x => x.MessageId).IsUnique();
            // 分发器轮询：按状态 + 下次投递时间查询
            e.HasIndex(x => new { x.Status, x.NextRetryTime });
        });

        modelBuilder.Entity<MessageSubscription>(e =>
        {
            e.ToTable("MessageSubscription");
            e.Property(x => x.EventName).HasMaxLength(200).IsRequired();
            e.Property(x => x.CallbackUrl).HasMaxLength(500).IsRequired();
            e.Property(x => x.ServiceName).HasMaxLength(100);
            e.HasIndex(x => new { x.EventName, x.CallbackUrl }).IsUnique();
        });

        modelBuilder.Entity<MessageIdempotency>(e =>
        {
            e.ToTable("MessageIdempotency");
            e.Property(x => x.ConsumerUrl).HasMaxLength(500).IsRequired();
            e.HasIndex(x => new { x.MessageId, x.ConsumerUrl }).IsUnique();
        });
    }
}

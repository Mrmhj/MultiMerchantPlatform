using NotificationService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace NotificationService.Infrastructure.Persistence;

/// <summary>
/// 通知数据库上下文（MMP_Notification 库）。
/// 通知为平台级服务：站内信按用户隔离（查询由处理器显式按 UserId 过滤），
/// 模板/短信/Push 不按商户隔离；Notification.MerchantId 为业务归属标记（null = 平台级）。
/// </summary>
public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    /// <summary>站内信通知表</summary>
    public DbSet<Notification> Notifications => Set<Notification>();

    /// <summary>通知模板表</summary>
    public DbSet<NotificationTemplate> Templates => Set<NotificationTemplate>();

    /// <summary>短信发送记录表</summary>
    public DbSet<SmsMessage> SmsMessages => Set<SmsMessage>();

    /// <summary>App Push 推送记录表</summary>
    public DbSet<PushMessage> PushMessages => Set<PushMessage>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("Notifications");
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            e.Property(x => x.BizType).HasMaxLength(50);
            e.Property(x => x.BizId).HasMaxLength(100);
            // 用户收件箱查询 + 未读数统计 + 平台/商户筛选
            e.HasIndex(x => new { x.UserId, x.IsDeleted, x.CreatedAt });
            e.HasIndex(x => new { x.UserId, x.IsRead, x.IsDeleted });
            e.HasIndex(x => new { x.MerchantId, x.IsDeleted, x.CreatedAt });
            e.HasIndex(x => x.BizType);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<NotificationTemplate>(e =>
        {
            e.ToTable("NotificationTemplates");
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.TitleTemplate).HasMaxLength(500).IsRequired();
            e.Property(x => x.BodyTemplate).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            // 模板编码唯一（发送端按 Code 定位）
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.IsActive);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<SmsMessage>(e =>
        {
            e.ToTable("SmsMessages");
            e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
            e.Property(x => x.Content).HasMaxLength(500).IsRequired();
            e.Property(x => x.LastError).HasMaxLength(1000);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => x.Phone);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<PushMessage>(e =>
        {
            e.ToTable("PushMessages");
            e.Property(x => x.DeviceToken).HasMaxLength(256).IsRequired();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Content).HasMaxLength(1000).IsRequired();
            e.Property(x => x.LastError).HasMaxLength(1000);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => x.DeviceToken);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });
    }
}

using EmailService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EmailService.Infrastructure.Persistence;

/// <summary>
/// 邮件数据库上下文（MMP_Email 库）。
/// </summary>
public sealed class EmailDbContext(DbContextOptions<EmailDbContext> options) : DbContext(options)
{
    public DbSet<EmailMessage> Emails => Set<EmailMessage>();

    public DbSet<EmailTemplate> Templates => Set<EmailTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<EmailMessage>(e =>
        {
            e.ToTable("EmailMessage");
            e.Property(x => x.From).HasMaxLength(200).IsRequired();
            e.Property(x => x.To).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Cc).HasMaxLength(1000);
            e.Property(x => x.Bcc).HasMaxLength(1000);
            e.Property(x => x.Subject).HasMaxLength(500).IsRequired();
            e.Property(x => x.Body).IsRequired();
            e.Property(x => x.TemplateName).HasMaxLength(100);
            e.Property(x => x.LastError).HasMaxLength(2000);

            // 后台重试轮询索引 + 按收件人查询
            e.HasIndex(x => new { x.Status, x.NextRetryTime });
            e.HasIndex(x => x.To);
        });

        modelBuilder.Entity<EmailTemplate>(e =>
        {
            e.ToTable("EmailTemplate");
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.SubjectTemplate).HasMaxLength(500).IsRequired();
            e.Property(x => x.BodyTemplate).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => x.Name).IsUnique();
        });
    }
}

using LoggingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LoggingService.Infrastructure.Persistence;

/// <summary>
/// 日志数据库上下文（MMP_Infra 库）。
/// </summary>
public sealed class LoggingDbContext(DbContextOptions<LoggingDbContext> options) : DbContext(options)
{
    /// <summary>日志表</summary>
    public DbSet<LogEntry> Logs => Set<LogEntry>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LogEntry>(e =>
        {
            e.ToTable("Logs");
            e.Property(x => x.ServiceName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Level).HasMaxLength(20).IsRequired();
            e.Property(x => x.Message).HasMaxLength(4000);
            e.Property(x => x.Exception).HasMaxLength(8000);
            e.Property(x => x.TraceId).HasMaxLength(64);
            e.Property(x => x.SpanId).HasMaxLength(64);
            e.Property(x => x.Category).HasMaxLength(200);
            e.Property(x => x.PropertiesJson).HasMaxLength(8000);

            // 查询索引：时间倒序分页 + 常见过滤组合
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => new { x.ServiceName, x.Timestamp });
            e.HasIndex(x => new { x.Level, x.Timestamp });
            e.HasIndex(x => x.TraceId);
        });
    }
}

using Microsoft.EntityFrameworkCore;
using PerformanceService.Domain.Entities;

namespace PerformanceService.Infrastructure.Persistence;

/// <summary>
/// 性能监控数据库上下文（MMP_Infra 库，与 messaging / logging 共用）。
/// </summary>
public sealed class PerformanceDbContext(DbContextOptions<PerformanceDbContext> options) : DbContext(options)
{
    /// <summary>压测任务表</summary>
    public DbSet<LoadTestTask> LoadTestTasks => Set<LoadTestTask>();

    /// <summary>压测运行批次表</summary>
    public DbSet<LoadTestRun> LoadTestRuns => Set<LoadTestRun>();

    /// <summary>指标快照表</summary>
    public DbSet<MetricsSnapshot> MetricsSnapshots => Set<MetricsSnapshot>();

    /// <summary>告警记录表</summary>
    public DbSet<AlertRecord> AlertRecords => Set<AlertRecord>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LoadTestTask>(e =>
        {
            e.ToTable("LoadTestTasks");
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.TargetUrl).HasMaxLength(500).IsRequired();
            e.Property(x => x.HttpMethod).HasMaxLength(10).IsRequired();
            e.Property(x => x.BodyJson).HasMaxLength(8000);
            e.Property(x => x.HeadersJson).HasMaxLength(8000);
        });

        modelBuilder.Entity<LoadTestRun>(e =>
        {
            e.ToTable("LoadTestRuns");
            e.Property(x => x.TaskName).HasMaxLength(100).IsRequired();
            e.Property(x => x.TargetUrl).HasMaxLength(500).IsRequired();
            e.Property(x => x.ReportPath).HasMaxLength(500);
            e.Property(x => x.ErrorMessage).HasMaxLength(500);
            e.HasIndex(x => x.TaskId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<MetricsSnapshot>(e =>
        {
            e.ToTable("MetricsSnapshots");
            e.Property(x => x.ServiceName).HasMaxLength(100).IsRequired();
            e.Property(x => x.SourceJson).HasMaxLength(8000);
            e.HasIndex(x => new { x.ServiceName, x.CapturedAt });
            e.HasIndex(x => x.CapturedAt);
        });

        modelBuilder.Entity<AlertRecord>(e =>
        {
            e.ToTable("AlertRecords");
            e.Property(x => x.ServiceName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Message).HasMaxLength(500).IsRequired();
            e.HasIndex(x => new { x.ServiceName, x.Status });
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });
    }
}

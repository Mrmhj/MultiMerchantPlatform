using RiskService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace RiskService.Infrastructure.Persistence;

/// <summary>
/// 风控数据库上下文（MMP_Risk 库）。
/// 风控为平台级服务（admin 角色管理，内部接口 X-Internal-Key），规则/黑名单/案例不按商户隔离；
/// 规则与案例的 MerchantId 为业务归属字段（null = 全局），查询时由处理器显式过滤。
/// </summary>
public sealed class RiskDbContext(DbContextOptions<RiskDbContext> options) : DbContext(options)
{
    /// <summary>风控规则表</summary>
    public DbSet<RiskRule> RiskRules => Set<RiskRule>();

    /// <summary>风控事件流水表</summary>
    public DbSet<RiskEvent> RiskEvents => Set<RiskEvent>();

    /// <summary>风险案例表</summary>
    public DbSet<RiskCase> RiskCases => Set<RiskCase>();

    /// <summary>黑名单表</summary>
    public DbSet<BlacklistEntry> BlacklistEntries => Set<BlacklistEntry>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RiskRule>(e =>
        {
            e.ToTable("RiskRules");
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Scene).HasMaxLength(50).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasIndex(x => new { x.Scene, x.Enabled });
            e.HasIndex(x => new { x.MerchantId, x.Enabled });
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<RiskEvent>(e =>
        {
            e.ToTable("RiskEvents");
            e.Property(x => x.Scene).HasMaxLength(50).IsRequired();
            e.Property(x => x.Ip).HasMaxLength(64);
            e.Property(x => x.DeviceId).HasMaxLength(128);
            e.Property(x => x.PayloadJson).HasMaxLength(8000);
            // 规则引擎按「场景+维度键+时间窗口」统计 → 复合索引
            e.HasIndex(x => new { x.Scene, x.UserId, x.OccurredAt });
            e.HasIndex(x => new { x.Scene, x.Ip, x.OccurredAt });
            e.HasIndex(x => new { x.Scene, x.DeviceId, x.OccurredAt });
            e.HasIndex(x => new { x.Scene, x.MerchantId, x.OccurredAt });
            e.HasIndex(x => x.OccurredAt);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<RiskCase>(e =>
        {
            e.ToTable("RiskCases");
            e.Property(x => x.RuleName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Scene).HasMaxLength(50).IsRequired();
            e.Property(x => x.DimensionKey).HasMaxLength(128).IsRequired();
            e.Property(x => x.Ip).HasMaxLength(64);
            e.Property(x => x.DeviceId).HasMaxLength(128);
            e.Property(x => x.Source).HasMaxLength(20).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(500).IsRequired();
            e.Property(x => x.ResolutionNote).HasMaxLength(500);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => new { x.MerchantId, x.Status });
            e.HasIndex(x => new { x.RuleId, x.DimensionKey, x.Status });
            e.HasIndex(x => new { x.UserId, x.Status });
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<BlacklistEntry>(e =>
        {
            e.ToTable("BlacklistEntries");
            e.Property(x => x.TargetValue).HasMaxLength(128).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            // 同对象 + 同商户唯一（防重复拉黑）
            e.HasIndex(x => new { x.TargetType, x.TargetValue, x.MerchantId }).IsUnique();
            e.HasIndex(x => new { x.Enabled, x.ExpiresAt });
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        });
    }
}

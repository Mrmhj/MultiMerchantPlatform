using BuildingBlocks.MultiTenant;
using ImService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ImService.Infrastructure.Persistence;

/// <summary>
/// 即时通讯数据库上下文（MMP_IM 库）。
/// 多租户隔离：会话 / 成员 / 消息按商户（HasQueryFilter），平台 admin / 内部接口（无商户上下文）可读全量。
/// </summary>
public sealed class ImDbContext(
    DbContextOptions<ImDbContext> options,
    ITenantProvider tenantProvider) : DbContext(options)
{
    private readonly ITenantProvider _tenantProvider = tenantProvider;

    /// <summary>会话表</summary>
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();

    /// <summary>会话成员表</summary>
    public DbSet<ChatSessionMember> ChatSessionMembers => Set<ChatSessionMember>();

    /// <summary>消息表</summary>
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ChatSession>(e =>
        {
            e.ToTable("ChatSessions");
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.LastMessagePreview).HasMaxLength(200);
            e.HasIndex(x => new { x.MerchantId, x.LastMessageAt });
            e.HasIndex(x => new { x.Type, x.Status });
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            // 多租户隔离：当前商户只可见自己的会话
            e.HasQueryFilter(s => _tenantProvider.CurrentMerchantId == null || s.MerchantId == _tenantProvider.CurrentMerchantId);
        });

        modelBuilder.Entity<ChatSessionMember>(e =>
        {
            e.ToTable("ChatSessionMembers");
            e.Property(x => x.UserName).HasMaxLength(50).IsRequired();
            // 一个用户在同一个会话中只能有一条成员记录
            e.HasIndex(x => new { x.SessionId, x.UserId }).IsUnique();
            // 按用户反查其参与的会话
            e.HasIndex(x => x.UserId);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            e.HasQueryFilter(m => _tenantProvider.CurrentMerchantId == null || m.MerchantId == _tenantProvider.CurrentMerchantId);
        });

        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.ToTable("ChatMessages");
            e.Property(x => x.SenderName).HasMaxLength(50).IsRequired();
            e.Property(x => x.Content).HasMaxLength(4000).IsRequired();
            e.HasIndex(x => new { x.SessionId, x.CreatedAt });
            e.HasIndex(x => new { x.SessionId, x.IsRead });
            e.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()");

            e.HasQueryFilter(m => _tenantProvider.CurrentMerchantId == null || m.MerchantId == _tenantProvider.CurrentMerchantId);
        });
    }
}

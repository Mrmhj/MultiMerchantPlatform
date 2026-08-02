using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Entities;
using ImService.Domain.Enums;

namespace ImService.Domain.Entities;

/// <summary>
/// 会话成员 — 记录用户与会话的从属关系（用户可同时参与多个会话）。
/// 多租户：与所属会话相同的商户隔离。
/// </summary>
public sealed class ChatSessionMember : MultiTenantEntity
{
    private ChatSessionMember() { } // EF Core

    /// <summary>创建会话成员（加入即生效）</summary>
    /// <param name="sessionId">所属会话 ID</param>
    /// <param name="merchantId">所属商户 ID</param>
    /// <param name="userId">用户 ID</param>
    /// <param name="userName">用户名称（快照）</param>
    /// <param name="role">成员角色（买家/商户客服/平台管理员/系统）</param>
    [SetsRequiredMembers]
    public ChatSessionMember(Guid sessionId, Guid merchantId, Guid userId, string userName, ChatMemberRole role)
    {
        SessionId = sessionId;
        MerchantId = merchantId;
        UserId = userId;
        UserName = (userName ?? string.Empty).Trim();
        Role = role;
    }

    /// <summary>所属会话 ID</summary>
    public Guid SessionId { get; private set; }

    /// <summary>用户 ID</summary>
    public Guid UserId { get; private set; }

    /// <summary>用户名称（快照）</summary>
    public string UserName { get; private set; } = string.Empty;

    /// <summary>成员角色</summary>
    public ChatMemberRole Role { get; private set; }
}

using System.Diagnostics.CodeAnalysis;
using BuildingBlocks.Core.Entities;
using BuildingBlocks.Core.Exceptions;
using ImService.Domain.Enums;

namespace ImService.Domain.Entities;

/// <summary>
/// 聊天会话 — 私聊（买家 ↔ 客服）或群聊（商户客服群）的聚合根。
/// 多租户：会话归属商户（MerchantId）隔离。
/// 状态机：Active（进行中）→ Closed（已关闭）。
/// </summary>
public sealed class ChatSession : MultiTenantEntity, IAggregateRoot
{
    private readonly List<ChatSessionMember> _members = [];

    private ChatSession() { } // EF Core

    /// <summary>创建会话（初始 Active，可空名称仅私聊）</summary>
    /// <param name="merchantId">所属商户 ID</param>
    /// <param name="type">会话类型（Private/Group）</param>
    /// <param name="name">会话名称（群聊必填，私聊可空）</param>
    /// <param name="now">创建时间（UTC）</param>
    [SetsRequiredMembers]
    public ChatSession(Guid merchantId, ChatSessionType type, string? name, DateTime now)
    {
        if (type == ChatSessionType.Group && string.IsNullOrWhiteSpace(name))
            throw new DomainException("群聊必须指定会话名称", "GROUP_NAME_REQUIRED");

        MerchantId = merchantId;
        Type = type;
        Name = (name ?? string.Empty).Trim();
        Status = ChatSessionStatus.Active;
        CreatedAt = now;
    }

    /// <summary>会话类型（Private/Group）</summary>
    public ChatSessionType Type { get; private set; }

    /// <summary>会话名称（群聊显示名，私聊可为空串）</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>会话状态（Active/Closed）</summary>
    public ChatSessionStatus Status { get; private set; }

    /// <summary>最后一条消息时间（无消息为 null）</summary>
    public DateTime? LastMessageAt { get; private set; }

    /// <summary>最后一条消息摘要（列表展示，最多 200 字符）</summary>
    public string LastMessagePreview { get; private set; } = string.Empty;

    /// <summary>成员列表</summary>
    public IReadOnlyList<ChatSessionMember> Members => _members;

    /// <summary>添加成员（幂等：已存在的成员跳过）</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="userName">用户名称（快照）</param>
    /// <param name="role">成员角色</param>
    /// <returns>是否新增（false 表示已存在）</returns>
    public bool AddMember(Guid userId, string userName, ChatMemberRole role)
    {
        if (_members.Any(m => m.UserId == userId))
            return false;

        _members.Add(new ChatSessionMember(Id, MerchantId, userId, userName, role));
        return true;
    }

    /// <summary>是否包含指定用户成员</summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>true 表示是会话成员</returns>
    public bool ContainsMember(Guid userId) => _members.Any(m => m.UserId == userId);

    /// <summary>关闭会话（Active → Closed）</summary>
    /// <param name="now">关闭时间（UTC）</param>
    public void Close(DateTime now)
    {
        if (Status != ChatSessionStatus.Active)
            throw new DomainException($"当前状态不允许关闭（{Status}）", "SESSION_STATE_INVALID");

        Status = ChatSessionStatus.Closed;
        UpdatedAt = now;
    }

    /// <summary>更新最后一条消息摘要（收发消息时调用）</summary>
    /// <param name="preview">消息摘要（自动截断 200 字符）</param>
    /// <param name="now">消息时间（UTC）</param>
    public void TouchMessage(string preview, DateTime now)
    {
        LastMessageAt = now;
        LastMessagePreview = (preview ?? string.Empty).Trim();
        if (LastMessagePreview.Length > 200)
            LastMessagePreview = LastMessagePreview[..200];
        UpdatedAt = now;
    }
}

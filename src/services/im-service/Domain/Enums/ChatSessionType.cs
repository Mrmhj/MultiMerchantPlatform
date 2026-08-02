namespace ImService.Domain.Enums;

/// <summary>
/// 会话类型：私聊（买家 ↔ 客服/用户）、群聊（商户客服群）。
/// </summary>
public enum ChatSessionType
{
    /// <summary>私聊（双人会话）</summary>
    Private = 1,

    /// <summary>群聊（多人会话，如商户客服群）</summary>
    Group = 2,
}

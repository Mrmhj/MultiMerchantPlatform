namespace ImService.Domain.Enums;

/// <summary>
/// 会话状态：进行中 / 已关闭。
/// </summary>
public enum ChatSessionStatus
{
    /// <summary>进行中（可收发消息）</summary>
    Active = 1,

    /// <summary>已关闭（仅可查看历史）</summary>
    Closed = 2,
}

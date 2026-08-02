using System.Collections.Concurrent;

namespace ImService.Application.Hubs;

/// <summary>
/// 用户在线连接管理器 — 维护 用户 ID ↔ 活跃连接 ID 的内存映射（进程内单机方案）。
/// 用于：判断用户是否在线（内部推送是否实时送达）、上线补推离线消息。
/// 集群部署时需替换为 Redis 等分布式存储（Phase 4 扩展项）。
/// </summary>
public sealed class UserConnectionManager
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _connections = new();

    /// <summary>记录连接上线</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="connectionId">连接 ID</param>
    public void OnConnected(Guid userId, string connectionId)
    {
        var set = _connections.GetOrAdd(userId, _ => []);
        lock (set)
        {
            set.Add(connectionId);
        }
    }

    /// <summary>记录连接下线</summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="connectionId">连接 ID</param>
    public void OnDisconnected(Guid userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var set))
            return;

        lock (set)
        {
            set.Remove(connectionId);
            if (set.Count == 0)
                _connections.TryRemove(userId, out _);
        }
    }

    /// <summary>用户是否在线（存在至少一个活跃连接）</summary>
    /// <param name="userId">用户 ID</param>
    /// <returns>true 表示在线</returns>
    public bool IsOnline(Guid userId)
        => _connections.TryGetValue(userId, out var set) && set.Count > 0;

    /// <summary>当前在线用户数（监控/调试用）</summary>
    public int OnlineCount => _connections.Count;
}

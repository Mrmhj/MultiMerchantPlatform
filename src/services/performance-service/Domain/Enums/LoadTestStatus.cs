namespace PerformanceService.Domain.Enums;

/// <summary>
/// 压测运行状态机：Queued（已入队）→ Running（执行中）→ Completed / Failed / Cancelled。
/// </summary>
public enum LoadTestStatus
{
    /// <summary>已入队，等待执行</summary>
    Queued = 0,

    /// <summary>执行中</summary>
    Running = 1,

    /// <summary>执行完成</summary>
    Completed = 2,

    /// <summary>执行失败（配置错误 / 引擎异常）</summary>
    Failed = 3,

    /// <summary>已取消（手动停止 / 服务关闭）</summary>
    Cancelled = 4,
}

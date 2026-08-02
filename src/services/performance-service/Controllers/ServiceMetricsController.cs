using Microsoft.AspNetCore.Mvc;
using PerformanceService.Application.Services;

namespace PerformanceService.Controllers;

/// <summary>
/// 进程指标暴露接口（内部）— 供 performance-service 监控采集器及未来其他微服务接入时使用。
/// 鉴权：X-Internal-Key 请求头校验（与各服务内部接口约定一致），不要求 JWT（供服务间调用）。
/// 标准 JSON schema：serviceName / capturedAt / managedMemoryMb / workingSetMb / cpuPercent /
/// gen0GcCount / gen1GcCount / gen2GcCount / threadPoolAvailable / threadPoolMax。
/// </summary>
[ApiController]
[Route("api/metrics")]
[Produces("application/json")]
public sealed class ServiceMetricsController(ProcessMetricsProvider metricsProvider, IConfiguration configuration) : ControllerBase
{
    /// <summary>获取本服务进程指标（内部端点，X-Internal-Key 校验）</summary>
    /// <param name="key">内部调用密钥（X-Internal-Key 请求头）</param>
    /// <returns>200 — 进程指标；401 — 密钥缺失或错误</returns>
    /// <response code="200">进程指标</response>
    /// <response code="401">密钥缺失或错误</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Metrics([FromHeader(Name = "X-Internal-Key")] string? key)
    {
        var expected = configuration["Internal:Key"] ?? string.Empty;
        if (string.IsNullOrEmpty(expected) || !string.Equals(key, expected, StringComparison.Ordinal))
            return Unauthorized(new { error = "无效的内部调用密钥", code = "INVALID_INTERNAL_KEY" });

        var metrics = metricsProvider.Capture();
        return Ok(new
        {
            serviceName = "performance-service",
            metrics.CapturedAt,
            managedMemoryMb = Math.Round(metrics.ManagedMemoryMb, 2),
            workingSetMb = Math.Round(metrics.WorkingSetMb, 2),
            cpuPercent = Math.Round(metrics.CpuPercent, 2),
            metrics.Gen0GcCount,
            metrics.Gen1GcCount,
            metrics.Gen2GcCount,
            metrics.ThreadPoolAvailable,
            metrics.ThreadPoolMax,
        });
    }
}

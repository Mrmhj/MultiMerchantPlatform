using System.Text;
using Microsoft.Extensions.Options;
using PerformanceService.Domain.Entities;
using PerformanceService.Infrastructure;

namespace PerformanceService.Application.Services;

/// <summary>
/// HTML 压测报告生成器 — 将压测运行统计渲染为自包含 HTML 文件（内联 CSS + SVG 图表，无外部依赖），
/// 输出到配置的报告目录（默认 E:\MultiMerchantPlatform\docs\reports）。
/// </summary>
public sealed class HtmlReportGenerator(IOptions<ReportOptions> reportOptions)
{
    /// <summary>生成压测报告</summary>
    /// <param name="run">压测运行批次（含统计结果）</param>
    /// <param name="stats">压测统计</param>
    /// <returns>报告相对文件名（如 loadtest-20260802-103000-xxx.html）</returns>
    public async Task<string> GenerateAsync(LoadTestRun run, LoadTestStatistics stats)
    {
        var directory = reportOptions.Value.Directory;
        Directory.CreateDirectory(directory);

        var fileName = $"loadtest-{run.CreatedAt:yyyyMMdd-HHmmss}-{Slug(run.TaskName)}.html";
        var fullPath = Path.Combine(directory, fileName);

        var html = BuildHtml(run, stats);
        await File.WriteAllTextAsync(fullPath, html, Encoding.UTF8);
        return fileName;
    }

    /// <summary>将任务名转换为安全文件名片段</summary>
    private static string Slug(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var slug = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
        return string.IsNullOrWhiteSpace(slug) ? "loadtest" : slug;
    }

    private static string BuildHtml(LoadTestRun run, LoadTestStatistics stats)
    {
        var successPercent = stats.TotalRequests > 0 ? stats.SuccessCount * 100.0 / stats.TotalRequests : 0;
        var failPercent = stats.TotalRequests > 0 ? stats.FailCount * 100.0 / stats.TotalRequests : 0;
        var maxLatency = Math.Max(stats.MaxLatencyMs, 1);

        double bar(double value) => Math.Clamp(value / maxLatency * 100, 1, 100);

        return $$"""
        <!DOCTYPE html>
        <html lang="zh-CN">
        <head>
        <meta charset="utf-8" />
        <title>压测报告 — {{HtmlEncode(run.TaskName)}}</title>
        <style>
          :root { --bg:#f5f7fa; --card:#fff; --text:#1f2d3d; --sub:#5e6d82; --line:#e4e7ed;
                  --primary:#409eff; --success:#67c23a; --danger:#f56c6c; --warning:#e6a23c; }
          * { box-sizing: border-box; margin: 0; padding: 0; }
          body { font-family: "Microsoft YaHei", "PingFang SC", sans-serif; background: var(--bg); color: var(--text); padding: 24px; }
          .wrap { max-width: 960px; margin: 0 auto; }
          h1 { font-size: 22px; margin-bottom: 4px; }
          .meta { color: var(--sub); font-size: 13px; margin-bottom: 20px; }
          .grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 14px; margin-bottom: 20px; }
          .card { background: var(--card); border: 1px solid var(--line); border-radius: 8px; padding: 16px; }
          .card .label { font-size: 12px; color: var(--sub); margin-bottom: 6px; }
          .card .value { font-size: 24px; font-weight: 600; }
          .card .unit { font-size: 12px; color: var(--sub); font-weight: 400; }
          .ok { color: var(--success); } .bad { color: var(--danger); } .warn { color: var(--warning); }
          table { width: 100%; border-collapse: collapse; background: var(--card); border-radius: 8px; overflow: hidden; }
          th, td { padding: 10px 14px; text-align: left; font-size: 13px; border-bottom: 1px solid var(--line); }
          th { background: #f0f2f5; color: var(--sub); font-weight: 600; }
          tr:last-child td { border-bottom: none; }
          .chart { background: var(--card); border: 1px solid var(--line); border-radius: 8px; padding: 18px; margin-top: 20px; }
          .chart h2 { font-size: 15px; margin-bottom: 12px; }
          .bars { display: flex; align-items: flex-end; gap: 24px; height: 200px; padding: 0 8px; }
          .bar-col { flex: 1; display: flex; flex-direction: column; align-items: center; justify-content: flex-end; height: 100%; }
          .bar { width: 100%; max-width: 70px; border-radius: 4px 4px 0 0; min-height: 2px; }
          .bar-label { margin-top: 8px; font-size: 12px; color: var(--sub); }
          .bar-value { font-size: 12px; font-weight: 600; margin-bottom: 4px; }
          .legend { display: flex; gap: 16px; margin-top: 10px; font-size: 12px; color: var(--sub); }
          .dot { display: inline-block; width: 10px; height: 10px; border-radius: 2px; margin-right: 4px; vertical-align: -1px; }
        </style>
        </head>
        <body>
        <div class="wrap">
          <h1>压测报告 — {{HtmlEncode(run.TaskName)}}</h1>
          <div class="meta">目标：{{HtmlEncode(run.TargetUrl)}} ｜ 并发：{{run.Concurrency}} ｜ 时长：{{run.DurationSeconds}}s ｜ 生成时间：{{run.FinishedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "—"}}</div>

          <div class="grid">
            <div class="card"><div class="label">总请求数</div><div class="value">{{stats.TotalRequests:N0}}</div></div>
            <div class="card"><div class="label">QPS</div><div class="value">{{stats.Qps:F1}}</div></div>
            <div class="card"><div class="label">平均延迟</div><div class="value">{{stats.AvgLatencyMs:F1}}<span class="unit"> ms</span></div></div>
            <div class="card"><div class="label">错误率</div><div class="value {{(stats.ErrorRatePercent > 5 ? "bad" : "ok")}}">{{stats.ErrorRatePercent:F2}}%</div></div>
          </div>

          <table>
            <tr><th>指标</th><th>值</th><th>指标</th><th>值</th></tr>
            <tr><td>成功请求</td><td class="ok">{{stats.SuccessCount:N0}}（{{successPercent:F1}}%）</td><td>失败请求</td><td class="{{(stats.FailCount > 0 ? "bad" : "")}}">{{stats.FailCount:N0}}（{{failPercent:F1}}%）</td></tr>
            <tr><td>P50 延迟</td><td>{{stats.P50Ms:F1}} ms</td><td>P95 延迟</td><td>{{stats.P95Ms:F1}} ms</td></tr>
            <tr><td>P99 延迟</td><td>{{stats.P99Ms:F1}} ms</td><td>最大延迟</td><td>{{stats.MaxLatencyMs:F1}} ms</td></tr>
          </table>

          <div class="chart">
            <h2>延迟分布（毫秒）</h2>
            <div class="bars">
              <div class="bar-col"><div class="bar-value">{{stats.AvgLatencyMs:F1}}</div><div class="bar" style="height:{{bar(stats.AvgLatencyMs)}}%;background:var(--primary)"></div><div class="bar-label">平均</div></div>
              <div class="bar-col"><div class="bar-value">{{stats.P50Ms:F1}}</div><div class="bar" style="height:{{bar(stats.P50Ms)}}%;background:var(--success)"></div><div class="bar-label">P50</div></div>
              <div class="bar-col"><div class="bar-value">{{stats.P95Ms:F1}}</div><div class="bar" style="height:{{bar(stats.P95Ms)}}%;background:var(--warning)"></div><div class="bar-label">P95</div></div>
              <div class="bar-col"><div class="bar-value">{{stats.P99Ms:F1}}</div><div class="bar" style="height:{{bar(stats.P99Ms)}}%;background:var(--danger)"></div><div class="bar-label">P99</div></div>
              <div class="bar-col"><div class="bar-value">{{stats.MaxLatencyMs:F1}}</div><div class="bar" style="height:{{bar(stats.MaxLatencyMs)}}%;background:#909399"></div><div class="bar-label">最大</div></div>
            </div>
            <div class="legend">
              <span><span class="dot" style="background:var(--primary)"></span>平均</span>
              <span><span class="dot" style="background:var(--success)"></span>P50</span>
              <span><span class="dot" style="background:var(--warning)"></span>P95</span>
              <span><span class="dot" style="background:var(--danger)"></span>P99</span>
            </div>
          </div>
        </div>
        </body>
        </html>
        """;
    }

    private static string HtmlEncode(string value) => System.Net.WebUtility.HtmlEncode(value);
}

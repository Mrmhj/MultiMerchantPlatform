# performance-service 模块文档

> **文档路径**：`docs/modules/performance-service.md`
> **版本**：v6.3 · 2026-08-02 · **端口 8017 · 数据库 MMP_Infra（与 messaging/logging 共用）**
> **定位**：P2 平台支撑服务 — 压测引擎 + 微服务内存/CPU/GC/线程池监控 + 异常告警

---

## 一、职责概述

| 能力 | 说明 |
|------|------|
| **压测引擎** | 对任意微服务发起 HTTP 并发压测（URL/方法/并发/时长/请求体/请求头），实时统计 QPS、平均/P50/P95/P99/最大延迟、错误率 |
| **报告生成** | 压测完成自动生成自包含 HTML 报告（内联 CSS + 图表，无外部依赖），输出到 `E:\MultiMerchantPlatform\docs\reports\`，可经 API 下载 |
| **监控采集** | 定时轮询全部微服务：健康探测（可达性 + 响应时间）+ 拉取 `/api/metrics` 完整进程指标（内存/CPU/GC/线程池）；未暴露指标端点的服务降级为 HTTP 层监控 |
| **自身指标** | 采集 performance-service 自身进程指标作为参照基准（`ProcessMetricsProvider`：GC/Process/ThreadPool） |
| **告警评估** | 内存 / 响应时间 / 服务连续不可达 → Warning/Critical 告警；指标恢复自动关闭；手动关闭 API；notification-service 未建，先落库 + 日志（预留扩展点） |

---

## 二、技术架构

```
┌────────────────────────────────────────────────────────┐
│                    performance-service (8017)           │
│                                                        │
│  ┌─────────────────┐   ┌─────────────────────────────┐ │
│  │  压测引擎         │   │  监控采集器 MetricsCollector │ │
│  │ LoadTestEngine   │   │  (BackgroundService, 15s)   │ │
│  │ Channel<Guid>队列 │   │  ├─ 健康探测 GET {BaseUrl}/ │ │
│  │ + 并发 worker    │   │  ├─ 指标拉取 GET /api/metrics│ │
│  │ + 取消支持        │   │  └─ 自身进程指标采集         │ │
│  └───────┬─────────┘   └──────────┬──────────────────┘ │
│          │                        │                    │
│  ┌───────▼─────────┐   ┌──────────▼──────────────────┐ │
│  │ HTML 报告生成器   │   │ 告警评估 AlertEvaluator      │ │
│  │ (docs/reports/)  │   │ (阈值判断 → AlertRecords)    │ │
│  └─────────────────┘   └─────────────────────────────┘ │
│                                                        │
│  存储：MMP_Infra（LoadTestTasks/LoadTestRuns/          │
│        MetricsSnapshots/AlertRecords）                  │
└────────────────────────────────────────────────────────┘
```

### 分层（Mediator + CQRS 强制）

```
Controller → IMediator → ICommandHandler / IQueryHandler → Domain 实体（充血）→ PerformanceDbContext
```

- 写操作：Create/Update/Delete/SetEnabled LoadTestTask、Run/Stop LoadTest、Resolve Alert
- 读操作：任务/运行列表与详情、指标最新/历史、服务列表、告警列表
- 后台任务：`LoadTestEngine`（压测执行队列）、`MetricsCollector`（监控采集），均注册为单例 + HostedService

---

## 三、数据库设计（MMP_Infra 库）

### LoadTestTasks — 压测任务定义

| 字段 | 类型 | 说明 |
|------|------|------|
| Name | nvarchar(100) | 任务名称（2-100 字符） |
| TargetUrl | nvarchar(500) | 压测目标 URL（http/https 校验） |
| HttpMethod | nvarchar(10) | GET/POST/PUT/DELETE |
| Concurrency | int | 并发数（1-500） |
| DurationSeconds | int | 持续时间（1-3600 秒） |
| BodyJson / HeadersJson | nvarchar(8000) | 请求体 / 请求头（JSON，可选） |
| Enabled | bit | 是否启用（停用不可启动） |

### LoadTestRuns — 压测运行批次

| 字段 | 类型 | 说明 |
|------|------|------|
| TaskId / TaskName / TargetUrl | — | 任务快照（删除任务后仍可追溯） |
| HttpMethod / BodyJson / HeadersJson | — | 执行配置快照 |
| Concurrency / DurationSeconds | — | 并发与时长快照 |
| Status | int | Queued→Running→Completed / Failed / Cancelled |
| TotalRequests / SuccessCount / FailCount | bigint | 请求统计 |
| Qps / AvgLatencyMs / P50Ms / P95Ms / P99Ms / MaxLatencyMs | float | 延迟统计 |
| ErrorRatePercent | float | 错误率（0-100） |
| ReportPath | nvarchar(500) | HTML 报告相对文件名 |
| ErrorMessage | nvarchar(500) | 失败原因 |

### MetricsSnapshots — 指标快照

| 字段 | 类型 | 说明 |
|------|------|------|
| ServiceName | nvarchar(100) | 服务名 |
| CapturedAt | datetime2 | 采样时间（UTC） |
| IsUp / ResponseMs | — | 可达性 + 健康探测响应时间 |
| ManagedMemoryMb / WorkingSetMb / CpuPercent | float? | 进程级指标（目标未暴露为 null） |
| Gen0GcCount / Gen1GcCount / Gen2GcCount | bigint? | GC 计数 |
| ThreadPoolAvailable / ThreadPoolMax | int? | 线程池 |
| SourceJson | nvarchar(8000) | 原始指标 JSON（≤8000 截断） |

### AlertRecords — 告警记录

| 字段 | 类型 | 说明 |
|------|------|------|
| ServiceName | nvarchar(100) | 服务名 |
| MetricType | int | ServiceDown / ResponseTime / Memory / ErrorRate |
| Level | int | Warning / Critical |
| CurrentValue / Threshold | float | 当前值 / 阈值 |
| Status | int | Open → Resolved |
| Message | nvarchar(500) | 告警说明 |

索引：Runs(TaskId / Status / CreatedAt)、Snapshots(ServiceName+CapturedAt / CapturedAt)、Alerts(ServiceName+Status / Status+CreatedAt)

---

## 四、API 一览（网关前缀 `/api/performance/**`，admin 角色）

### 压测任务（LoadTestsController）

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/performance/load-tests` | 创建压测任务 |
| PUT | `/api/performance/load-tests/{id}` | 更新任务 |
| DELETE | `/api/performance/load-tests/{id}` | 删除任务 |
| PUT | `/api/performance/load-tests/{id}/enabled?enabled=` | 启用 / 停用 |
| POST | `/api/performance/load-tests/{id}/run` | 启动压测（创建 Queued 批次入队） |
| POST | `/api/performance/load-tests/runs/{runId}/stop` | 停止运行（→ Cancelled） |
| GET | `/api/performance/load-tests` | 任务列表（分页） |
| GET | `/api/performance/load-tests/runs?taskId=&status=` | 运行历史（分页，可按任务/状态过滤） |
| GET | `/api/performance/load-tests/runs/{runId}` | 运行详情（统计结果） |
| GET | `/api/performance/load-tests/runs/{runId}/report` | 下载 HTML 报告 |

### 监控指标（MetricsController）

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/performance/metrics/latest?service=` | 每服务最新指标快照 |
| GET | `/api/performance/metrics/history?service=&from=&to=&limit=` | 指标历史（趋势，时间升序） |
| GET | `/api/performance/metrics/services` | 已监控服务列表 |
| POST | `/api/performance/metrics/collect` | 手动触发一轮采集（调试/演示） |

### 告警（AlertsController）

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/performance/alerts?status=&service=&page=` | 告警列表（分页） |
| PUT | `/api/performance/alerts/{id}/resolve` | 手动关闭告警 |

### 内部端点（ServiceMetricsController，X-Internal-Key 校验，非 JWT）

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/metrics` | 本服务进程指标（标准 schema，供采集器及其他服务接入） |

> **标准指标 JSON schema**（其他微服务接入监控只需按此格式暴露 `/api/metrics` 并校验 `X-Internal-Key`）：
> `{ serviceName, capturedAt, managedMemoryMb, workingSetMb, cpuPercent, gen0GcCount, gen1GcCount, gen2GcCount, threadPoolAvailable, threadPoolMax }`

---

## 五、配置说明（appsettings.json）

| 配置节 | 说明 | 默认 |
|--------|------|------|
| `ConnectionStrings:PerformanceDb` | MMP_Infra 连接串 | `Server=localhost;Database=MMP_Infra;...` |
| `Jwt` | 与 identity-service 同密钥（admin 角色校验） | — |
| `Internal:Key` | 内部调用密钥（X-Internal-Key） | MMP-Internal-Key-2026 |
| `Reports:Directory` | 压测报告输出目录 | `E:\MultiMerchantPlatform\docs\reports` |
| `LoadTest:MaxConcurrency / MaxDurationSeconds` | 压测限制 | 500 / 3600 |
| `Monitoring:IntervalSeconds` | 采集间隔（秒） | 15 |
| `Monitoring:CollectSelfMetrics` | 是否采集自身指标 | true |
| `Monitoring:Targets[]` | 监控目标清单（ServiceName/BaseUrl/MetricsPath/HealthPath/IsInternal） | 17 项（网关 + 16 服务） |
| `Monitoring:Alerts` | 阈值：MemoryWarningMb/CriticalMb、ResponseTimeWarningMs/CriticalMs、ErrorRateThresholdPercent、DownThresholdConsecutive | 1024/2048、1000/3000、5、3 |

---

## 六、关键实现说明

### 压测引擎（LoadTestEngine）

- **队列**：`Channel<Guid>` 无界队列 + BackgroundService 消费，创建运行批次后 `Enqueue(runId)` 异步执行，接口立即返回
- **并发模型**：`Enumerable.Range(0, concurrency)` 生成 worker 任务，每个 worker 独立循环发送请求直到持续时间到期；统计用 `Interlocked` + `ConcurrentBag<double>`（线程安全）
- **取消**：`CancellationTokenSource.CreateLinkedTokenSource(stoppingToken)`，手动停止（Stop API → Cancel）+ 进程关闭（HostedService StopAsync）双通道；取消后批次落库为 Cancelled
- **单请求超时**：命名 HttpClient `loadtest` 超时 100s 兜底；总时长由引擎按 DurationSeconds 控制
- **请求头**：`{"name":"value"}` JSON 解析，body 仅 POST/PUT 生效（application/json）

### 监控采集器（MetricsCollector）

- 每 `IntervalSeconds`（15s）一轮：`Parallel.ForEachAsync` 并行探测全部目标 → 采集自身 → 快照批量入库 → AlertEvaluator 评估
- **健康探测**：`GET {BaseUrl}{HealthPath}`，任何 HTTP 响应（含 404/401）视为可达；网络异常/超时视为不可达（ResponseMs=-1）
- **指标拉取**：`GET {BaseUrl}/api/metrics`（IsInternal 时携带 X-Internal-Key），200 解析标准 JSON 得完整进程指标；404/异常 → 降级仅 HTTP 层（进程级字段为 null）
- **连续宕机计数**：`ConcurrentDictionary<string,int>` 维护各服务连续不可达次数，达到 `DownThresholdConsecutive`（3 次 ≈ 45s）才开 ServiceDown 告警，防抖动误报

### 告警评估（AlertEvaluator）

- 同键（服务+指标类型）Open 告警去重：已存在不重复建；指标回落/服务恢复 → 自动 Resolve 并记录恢复时间
- 通知扩展点：当前以 ILogger 输出 + AlertRecords 落库，future 接入 notification-service（注释标注）

---

## 七、验收记录（v6.3）

- 全量编译 0 警告 0 错误（28 项目，含 AspireHost）
- 冒烟测试 `tests/smoke-performance.sh`：**31/31 通过**
  - 鉴权：无 token 401 / 非 admin 403（admin SQL 提权）/ 内部端点错误密钥 401
  - 压测：创建（含 URL/并发校验 400）→ 启动 Queued → Completed（总请求/QPS/P99/错误率 0）→ HTML 报告落盘 + 下载 200 → 长任务停止 Cancelled
  - 监控：连续 3 轮采集 → 每服务最新快照（含宕机服务 isUp=false）→ 服务列表 → ServiceDown 告警生成 → 手动关闭 Resolved
- 网关路由 `/api/performance/**` → 8017；AspireHost 编排已接入

---

## 八、相关文档

| 文档 | 路径 |
|------|------|
| 路线图 | `docs/PROJECT_PLAN.md`（Phase 3 Week 14） |
| 编码规范 | `docs/architecture/coding-standards.md` |
| 变更记录 | `docs/CHANGELOG.md`（v6.3） |
| 压测报告示例 | `docs/reports/loadtest-*.html`（自动生成） |

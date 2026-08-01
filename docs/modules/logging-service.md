# logging-service — 自封装日志管理微服务

> **所属阶段**：Phase 0 Week 2 · **优先级**：P0 · **端口**：8011
> **更新日期**：2026-08-02

## 一、职责

收集平台所有微服务的日志并统一存储、检索、统计（替代 Seq/ELK 等外部日志系统）：

- 批量接收各微服务上报的日志（与 `BuildingBlocks.Logging` 客户端契约一致）
- 日志持久化：SQL Server（MMP_Infra 库 `Logs` 表）
- 检索：按服务 / 级别 / 关键字 / 时间范围分页查询
- 统计：级别分布 / Top 服务 / 时间趋势（小时/天）/ 错误率
- 查询 TraceId 定位链路日志

## 二、核心设计

```
┌───────────────────────────────────────────────┐
│             logging-service (8011)             │
│                                               │
│  ┌──────────────┐  ┌───────────────────────┐  │
│  │ LogIngest    │  │ LogQuery / LogStats   │  │
│  │ POST /batch  │  │ GET /logs /log-stats  │  │
│  └──────┬───────┘  └──────────┬────────────┘  │
│  ┌──────▼─────────────────────▼────────────┐  │
│  │     SQL Server · MMP_Infra · Logs 表    │  │
│  │  索引: Timestamp / (Service,Timestamp)  │  │
│  │        (Level,Timestamp) / TraceId      │  │
│  └─────────────────────────────────────────┘  │
└───────────────┬───────────────────────────────┘
                │ 批量上报（客户端缓冲 5s 合并）
                ▼
        各微服务（BuildingBlocks.Logging）
        AddCentralizedLogging("order-service", "http://localhost:8011")
```

### 数据表（MMP_Infra 库）

| 列 | 类型 | 说明 |
|----|------|------|
| Id | uniqueidentifier | 主键（客户端生成） |
| ServiceName | nvarchar(100) | 来源服务名 |
| Level | nvarchar(20) | Trace/Debug/Info/Warning/Error/Critical |
| Message | nvarchar(4000) | 日志消息 |
| Exception | nvarchar(8000)? | 异常堆栈 |
| TraceId / SpanId | nvarchar(64)? | 链路追踪 |
| Category | nvarchar(200)? | Logger 类别 |
| PropertiesJson | nvarchar(8000)? | 附加属性（JSON） |
| Timestamp | datetime2 | 产生时间 |

索引：`(Timestamp)`、`(ServiceName, Timestamp)`、`(Level, Timestamp)`、`(TraceId)`

## 三、REST API

### 日志 `/api/logs`

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/logs/batch` | 批量上报（客户端定时调用） |
| GET | `/api/logs?serviceName=&level=&keyword=&from=&to=&page=&pageSize=` | 分页查询 |
| GET | `/api/logs/{id}` | 按 Id 查询详情 |

### 统计 `/api/log-stats`

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/log-stats/level-distribution?from=&to=` | 级别分布 |
| GET | `/api/log-stats/top-services?top=&from=&to=` | 日志量 Top N 服务 |
| GET | `/api/log-stats/trend?from=&to=&granularity=hour\|day` | 时间趋势（默认近 24h 按小时） |
| GET | `/api/log-stats/error-rate?from=&to=` | 错误率（Error+Critical 占比 %） |

### 网关入口（YARP）

```
/api/logs/**        → logging-service (8011)
/api/log-stats/**   → logging-service (8011)
/api/health/**      → logging-service (8011)
```

## 四、客户端接入（各微服务）

```csharp
// Program.cs — 一行接入（Phase 0 Week 1 已实现）
builder.Logging.AddCentralizedLogging("order-service", "http://localhost:8011");

// 业务代码 — 使用标准 ILogger
public class OrderService(ILogger<OrderService> logger)
{
    public async Task CreateOrderAsync(Order order, CancellationToken ct)
    {
        logger.LogInformation("创建订单 {OrderId}, 金额 {Amount}", order.Id, order.TotalAmount);
        // 异常时
        try { /* ... */ }
        catch (Exception ex) { logger.LogError(ex, "订单创建失败"); }
    }
}
```

**客户端机制**（`BuildingBlocks.Logging.CentralizedLoggerProvider`）：
- 内存缓冲 + 每 5 秒批量上报（减少 HTTP 开销）
- 上报失败自动重入队下次重试，不影响业务线程
- 缓冲上限 10,000 条（达到上限丢弃新日志，防止上报服务不可用时内存溢出）
- 上报请求 10 秒超时

## 五、配置说明（appsettings.json）

```json
{
  "ConnectionStrings": {
    "LoggingDb": "Server=localhost;Database=MMP_Infra;User Id=sa;Password=123456;TrustServerCertificate=True"
  }
}
```

## 六、项目结构

```
src/services/logging-service/
├── Program.cs                        # 入口 + 启动自动迁移（开发环境）
├── Domain/Entities/LogEntry.cs       # 日志实体
├── Infrastructure/Persistence/LoggingDbContext.cs
├── Application/
│   ├── LogIngestService.cs           # 批量写入
│   ├── LogQueryService.cs            # 分页查询
│   ├── LogStatsService.cs            # 统计（分布/Top/趋势/错误率）
│   └── DependencyInjection.cs
├── DTOs/LoggingDtos.cs
├── Controllers/                      # Logs / LogStats / Health
└── Migrations/                       # EF Core 迁移
```

## 七、已验证（冒烟测试）

| 场景 | 结果 |
|------|------|
| 健康检查（数据库连通） | ✅ healthy |
| 批量上报 5 条日志 | ✅ ingested=5 |
| 分页查询（时间倒序） | ✅ 5 条 |
| 按级别过滤（Error） | ✅ 1 条 |
| 级别分布 | ✅ Info 2 / Warning 1 / Error 1 / Critical 1 |
| Top 服务 | ✅ order-service 2 条居首 |
| 错误率 | ✅ 40% |
| 时间趋势（小时/天） | ✅ 正确聚合 |

## 八、已知限制与后续扩展

- **写入性能**：开发版用 EF AddRange；日志量大时替换为 SqlBulkCopy（建议单批 ≥ 5000 条时启用）
- **数据保留**：未做自动归档/清理策略，建议按天分区 + 定时删除过期日志（如保留 90 天）
- **全文检索**：Keyword 目前用 LIKE 模糊匹配；后续可换 SQL Full-Text 或引入搜索引擎
- **看板**：日志统计已提供 API，前端（Vue 3 + ECharts）对接展示日志看板（Phase 2 performance-service 一并做）
- **鉴权**：当前未加 JWT 认证（内网服务间调用），网关暴露公网时需补充

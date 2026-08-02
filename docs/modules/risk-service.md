# risk-service 模块文档

> **文档路径**：`docs/modules/risk-service.md`
> **版本**：v6.4 · 2026-08-02 · **端口 8018 · 数据库 MMP_Risk（独立库）**
> **定位**：P2 平台支撑服务 — 风控规则引擎（反刷单/反薅羊毛/撞库防护）+ 黑名单 + 风险案例处置

---

## 一、职责概述

| 能力 | 说明 |
|------|------|
| **规则引擎** | 对业务上报行为事件按「场景 + 维度 + 时间窗口」聚合统计，超过阈值即命中，实时生成风险案例 |
| **事件上报** | 内部接口接收各微服务上报行为事件（下单/领券/登录失败/评价），落库 + 实时评估（X-Internal-Key 校验） |
| **风控决策** | 业务方关键操作（下单/领券）前调用决策接口：黑名单命中或存在未处置 Block 级案例 → 拦截，否则放行 |
| **黑名单** | 用户 / IP / 设备 三级黑名单，支持过期时间与商户维度；加入后决策接口直接拦截 |
| **案例处置** | 风险案例状态机：Open → Reviewing → Resolved / FalsePositive，平台风控人员闭环处置 |
| **默认规则** | 首次启动自动种子 5 条反刷单典型规则（高频下单/领券/登录失败/评价），幂等可复用 |

---

## 二、技术架构

```
┌────────────────────────────────────────────────────────┐
│                    risk-service (8018)                  │
│                                                        │
│  ┌─────────────────┐      ┌─────────────────────────┐  │
│  │ 内部接口          │      │ 规则引擎 RiskRuleEngine  │  │
│  │ /api/risk/       │      │ (场景+维度+窗口聚合评估)  │  │
│  │   internal/      │ ───► │  内存批计数 + DB 窗口计数 │  │
│  │   events(上报)    │      │  + 黑名单拦截检查         │  │
│  │   internal/decide│      └───────────┬─────────────┘  │
│  └─────────────────┘                  │               │
│                                        ▼               │
│  ┌─────────────────┐      ┌─────────────────────────┐  │
│  │ 平台接口 (admin) │      │ RiskEvents(事件流水)     │  │
│  │ 规则 CRUD/启停    │      │ RiskCases(风险案例)      │  │
│  │ 案例复核/处置      │      │ RiskRules(规则配置)      │  │
│  │ 黑名单/事件/概览   │      │ BlacklistEntries(黑名单) │  │
│  └─────────────────┘      └─────────────────────────┘  │
│                       存储：MMP_Risk（独立库）            │
└────────────────────────────────────────────────────────┘
```

### 分层（Mediator + CQRS 强制）

```
Controller → IMediator → ICommandHandler / IQueryHandler → Domain 实体（充血）→ RiskDbContext
```

- 写操作：SubmitEvents（上报+评估）、Decide（决策）、规则 Create/Update/Delete/SetEnabled、
  案例 StartReview/Resolve/FalsePositive、黑名单 Add/Remove/SetEnabled
- 读操作：规则/案例/事件/黑名单列表（分页过滤）、概览统计
- 引擎：`RiskRuleEngine`（Scoped）— 批量事件内存预计数 + 数据库窗口历史计数合并，避免批量同键漏统计

---

## 三、数据库设计（MMP_Risk 库）

### RiskRules — 风控规则配置

| 字段 | 类型 | 说明 |
|------|------|------|
| Name | nvarchar(100) | 规则名称（2-100 字符） |
| Scene | nvarchar(50) | 场景编码（ORDER_SUBMIT / COUPON_CLAIM / LOGIN_FAIL / REVIEW_SUBMIT） |
| Dimension | int | 统计维度：User=0 / Ip=1 / Device=2 / Merchant=3 |
| WindowSeconds | int | 时间窗口（1-86400 秒） |
| Threshold | int | 窗口内命中阈值（1-100000 次） |
| Disposition | int | 处置级别：Watch=0（观察）/ Block=1（拦截） |
| MerchantId | uniqueidentifier? | null = 全局规则（所有商户），非空 = 仅该商户 |
| Description | nvarchar(500)? | 规则说明 |
| Enabled | bit | 是否启用（停用不参与匹配） |

### RiskEvents — 风控事件流水（只追加）

| 字段 | 类型 | 说明 |
|------|------|------|
| Scene | nvarchar(50) | 场景编码 |
| UserId / MerchantId | uniqueidentifier? | 用户 / 商户 |
| Ip | nvarchar(64)? | 客户端 IP |
| DeviceId | nvarchar(128)? | 设备 ID |
| PayloadJson | nvarchar(8000)? | 附加载荷（订单号/商品ID 等） |
| OccurredAt | datetime2 | 事件发生时间（UTC） |

索引：`(Scene, UserId/Ip/DeviceId/MerchantId, OccurredAt)` 四组复合索引（引擎窗口统计用）、`OccurredAt`

### RiskCases — 风险案例

| 字段 | 类型 | 说明 |
|------|------|------|
| RuleId / RuleName | — | 命中规则（快照；黑名单拦截 RuleId=null） |
| Scene / Dimension / DimensionKey | — | 命中场景 / 维度 / 维度键（快照） |
| UserId / MerchantId / Ip / DeviceId | — | 关联对象（冗余快照） |
| OccurredCount / Threshold | int | 窗口内次数 / 规则阈值（快照） |
| Disposition | int | 处置级别（快照） |
| Source | nvarchar(20) | 来源：RULE_HIT / BLACKLIST |
| Summary | nvarchar(500) | 风险摘要（快照，如「60秒内下单5次」） |
| Status | int | Open=0 → Reviewing=1 → Resolved=2 / FalsePositive=3 |
| ResolutionNote / ResolvedAt | — | 处置备注 / 处置时间 |

### BlacklistEntries — 黑名单

| 字段 | 类型 | 说明 |
|------|------|------|
| TargetType | int | User=0 / Ip=1 / Device=2 |
| TargetValue | nvarchar(128) | 对象值（用户 ID 字符串 / IP / 设备 ID） |
| Reason | nvarchar(500) | 拉黑原因 |
| ExpiresAt | datetime2? | 过期时间（null = 永久） |
| MerchantId | uniqueidentifier? | null = 平台全局，非空 = 该商户 |
| Enabled | bit | 是否启用 |

唯一索引：`(TargetType, TargetValue, MerchantId)` 防重复拉黑

---

## 四、API 一览

### 内部接口（InternalRiskController，X-Internal-Key 校验，非 JWT）

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/risk/internal/events` | 批量上报事件（落库 + 规则引擎实时评估，返回 Submitted/Hits/Cases/Blocked） |
| POST | `/api/risk/internal/decide` | 风控决策（黑名单 + 未处置 Block 案例 → 拦截；否则放行） |

### 平台接口（AdminRiskController，admin 角色）

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/risk/overview` | 概览（启用/总规则数、黑名单数、待处置/复核中案例、今日事件与命中） |
| GET | `/api/risk/rules?page=&pageSize=&scene=&enabled=` | 规则列表（分页过滤） |
| POST | `/api/risk/rules` | 创建规则 |
| PUT | `/api/risk/rules/{id}` | 更新规则 |
| DELETE | `/api/risk/rules/{id}` | 删除规则 |
| PUT | `/api/risk/rules/{id}/enabled?enabled=` | 启用 / 停用规则 |
| GET | `/api/risk/cases?page=&pageSize=&status=&scene=&merchantId=&disposition=` | 案例列表（分页过滤） |
| POST | `/api/risk/cases/{id}/review` | 开始复核（Open → Reviewing） |
| POST | `/api/risk/cases/{id}/resolve` | 确认风险（→ Resolved） |
| POST | `/api/risk/cases/{id}/false-positive` | 标记误报（→ FalsePositive） |
| GET | `/api/risk/blacklist?page=&pageSize=&targetType=&enabled=` | 黑名单列表 |
| POST | `/api/risk/blacklist` | 加入黑名单（同对象已存在则更新原因/有效期并启用） |
| DELETE | `/api/risk/blacklist/{id}` | 移除黑名单 |
| PUT | `/api/risk/blacklist/{id}/enabled?enabled=` | 启用 / 停用黑名单 |
| GET | `/api/risk/events?page=&pageSize=&scene=&userId=&merchantId=` | 事件流水分页查询 |

> 网关路由：`/api/risk/**` → 8018；`/api/health` → 8018（risk-health 路由）

---

## 五、配置说明（appsettings.json）

| 配置节 | 说明 | 默认 |
|--------|------|------|
| `ConnectionStrings:RiskDb` | MMP_Risk 连接串 | `Server=localhost;Database=MMP_Risk;...` |
| `Jwt` | 与 identity-service 同密钥（admin 角色校验） | — |
| `Internal:Key` | 内部调用密钥（X-Internal-Key） | MMP-Internal-Key-2026 |
| `Risk:EventRetentionDays` | 事件保留天数（清理任务预留） | 30 |
| `Risk:MaxEventsPerBatch` | 单批上报事件上限（预留） | 1000 |

---

## 六、关键实现说明

### 规则引擎（RiskRuleEngine）

- **匹配流程**：事件上报 → 加载启用规则 + 有效黑名单 → 逐事件黑名单检查（10 分钟内同键去重）→
  按 (规则, 维度键) 内存批计数 + 数据库窗口历史计数合并 → 总次数 ≥ 阈值 → 命中
- **统计口径**：事件先 `Add`（未保存）后查询，批量内同键事件由内存计数补齐，杜绝漏统计；
  库内计数按 Dimension 分支内联 EF 可翻译表达式（自定义方法无法翻译成 SQL）
- **命中去重**：同规则 + 同维度键 + 窗口内未处置（Open/Reviewing）案例已存在 → 追加 OccurredCount，
  不重复建单；已处置案例不阻止新案例（重新命中重新建档）
- **黑名单拦截**：命中生成 `BLACKLIST` 来源的 Block 案例（阈值/次数快照为 1/1）

### 风控决策（Decide）

- 黑名单（启用且未过期，匹配用户/IP/设备 + 商户范围）→ 拦截
- 未处置 Block 级案例（用户维度优先，其次 IP）→ 拦截
- 均无 → 放行；业务方集成：下单/领券前调用，Allow=false 时阻止操作

### 默认规则种子（幂等）

| 规则 | 场景 | 维度 | 窗口 | 阈值 | 处置 |
|------|------|------|------|------|------|
| 高频下单（同用户） | ORDER_SUBMIT | User | 60s | 5 | Watch |
| 高频下单（同 IP） | ORDER_SUBMIT | Ip | 60s | 10 | Block |
| 高频领券（同用户） | COUPON_CLAIM | User | 60s | 10 | Watch |
| 高频登录失败（同 IP） | LOGIN_FAIL | Ip | 300s | 10 | Block |
| 高频评价（同用户） | REVIEW_SUBMIT | User | 60s | 5 | Watch |

---

## 七、验收记录（v6.4）

- 全量编译 0 CS 警告 0 错误（29 项目，含 AspireHost；仅环境性 NU1900 NuGet 缓存权限警告）
- 冒烟测试 `tests/smoke-risk.sh`：**36/36 通过**
  - 鉴权：无 token 401 / 买家调平台接口 403 / 内部接口错误密钥 401
  - 规则 CRUD：默认规则种子 → 创建/更新/启停 → 参数校验 400 → 删除
  - 规则命中：同 IP 30s 窗口 3 次阈值，事件 1/2 无命中、事件 3 命中 Block 案例（hits=1）
  - 决策：命中后拦截（allow=false + 规则摘要）、正常用户放行（allow=true）
  - 案例处置：复核 → 确认风险 → 误报（状态机闭环）
  - 黑名单：加入 → 决策拦截 → 停用放行 → 重复拉黑更新 → 移除
  - 事件流水查询 + 概览统计
- 网关路由 `/api/risk/**` → 8018；AspireHost 编排已接入；冒烟数据已清理（仅保留 5 条默认规则）

---

## 八、相关文档

| 文档 | 路径 |
|------|------|
| 路线图 | `docs/PROJECT_PLAN.md`（Phase 3 Week 14） |
| 编码规范 | `docs/architecture/coding-standards.md` |
| 变更记录 | `docs/CHANGELOG.md`（v6.4） |

# 多商户电商平台 — 架构结构文档

> **版本**：v7.3 · 2026-08-02 · Phase 4 Week 20
> **部署形态**：本机 IIS 托管（21 后端 + 4 前端站点）
> **关联文档**：PROJECT_PLAN.md（路线图）/ docs/architecture/coding-standards.md（编码规范）/ docs/guides/local-deployment.md（部署指南）/ docs/database/database-catalog.md（库表总览）

---

## 一、总体架构

```
                          ┌──────────────────────────────────────────────┐
                          │               前端（4 站点）                  │
                          │  web-customer:5173  web-merchant:5174        │
                          │  mobile-app:5175     web-admin:5177          │
                          └───────────────┬──────────────────────────────┘
                                          │ /api /hub（URL Rewrite + ARR 代理）
                                          ▼
                          ┌──────────────────────────────────────────────┐
                          │        YARP 网关 ApiGateway :8000            │
                          │  · 路由转发（服务前缀剥离）                    │
                          │  · 入口限流（RateLimiter：并发→固定窗口→令牌桶）│
                          └───────┬──────────┬───────────┬──────────────┘
                                  ▼          ▼           ▼
                          ┌──────────────────────────────────────────────┐
                          │            20 个微服务（IIS 站点）            │
                          │  8001-8020 · 每服务独立应用池                 │
                          └──────────────────────────────────────────────┘
                                          │
              ┌───────────────────────────┼─────────────────────────────┐
              ▼                           ▼                             ▼
   ┌──────────────────┐      ┌──────────────────┐        ┌────────────────────┐
   │  SQL Server 2025 │      │  Redis 5.0.14    │        │  messaging 总线     │
   │  18 库 MMP_*     │      │  (Windows 服务)  │        │  (SQL Server 持久化)│
   │  sa/123456       │      │  6379 密码保护   │        │  Outbox + 订阅      │
   └──────────────────┘      └──────────────────┘        └────────────────────┘
```

## 二、技术栈

| 层 | 技术 | 说明 |
|---|---|---|
| 后端 | .NET 10 / C# 13 | 20 微服务 + YARP 网关 |
| 前端 | Vue 3.5 + Vite 8 + TS 5 + Element Plus | 4 个 Web 应用 |
| 移动端 | uni-app（Vue 3） | mobile-app H5 |
| 桌面端 | Electron 33 + Vue 3 | desktop-app（本地分发，非 IIS） |
| 数据库 | SQL Server 2025 | 18 个业务库（MMP_* 前缀） |
| 缓存 | Redis 5.0.14（tporadowski Windows 版） | 热数据缓存 + 秒杀预扣 + 分布式锁 |
| 消息 | 自研 messaging-service | SQL Server Outbox 持久化，事件总线 |
| 通信 | HTTP（IServiceClient）+ Polly v8 弹性 | 服务间调用（可切 gRPC） |
| 鉴权 | JWT（MapInboundClaims=false，role 短名） | identity 签发，多租户 X-Merchant-Id |

## 三、服务清单与职责

### 3.1 网关（入口）

| 服务 | 端口 | 职责 |
|---|---|---|
| ApiGateway（YARP） | 8000 | 统一入口：路由转发 / 限流（RateLimiter）/ 原 Host 透传 |

### 3.2 核心业务域（Phase 1）

| 服务 | 端口 | 库 | 职责 |
|---|---|---|---|
| identity-service | 8001 | MMP_Identity | 注册/登录/JWT/失败锁定 |
| merchant-service | 8002 | MMP_Merchant | 入驻/审核/店铺 |
| product-service | 8003 | MMP_Product | 分类/商品/SKU/上下架 + C 端公开（Redis 缓存） |
| order-service | 8004 | MMP_Order | 跨商户拆单/状态机/秒杀异步下单 |
| pay-service | 8005 | MMP_Pay | 支付单/模拟支付/退款 |
| stock-service | 8006 | MMP_Stock | 库存预占/扣减/释放 |

### 3.3 交易辅助域（Phase 2）

| 服务 | 端口 | 库 | 职责 |
|---|---|---|---|
| cart-service | 8007 | MMP_Cart | 购物车（买家隔离/同 SKU 合并） |
| search-service | 8008 | MMP_Search | 商品搜索索引 |
| promotion-service | 8009 | MMP_Promotion | 优惠券/满减/**秒杀**（Redis 预扣+异步下单） |
| messaging-service | 8010 | MMP_Infra | 消息总线（Outbox/订阅） |
| logging-service | 8011 | MMP_Infra | 日志批量上报/查询 |
| review-service | 8012 | MMP_Review | 商品评价 |
| logistics-service | 8013 | MMP_Logistics | 运单/轨迹 |
| settlement-service | 8014 | MMP_Settlement | 佣金/结算单 |
| email-service | 8015 | MMP_Email | 邮件（MailKit/DryRun） |

### 3.4 增值服务域（Phase 3-4）

| 服务 | 端口 | 库 | 职责 |
|---|---|---|---|
| im-service | 8016 | MMP_IM | 即时通讯（SignalR） |
| performance-service | 8017 | MMP_Infra | 压测 + 监控 + 告警 |
| risk-service | 8018 | MMP_Risk | 风控/反刷单规则引擎 |
| notification-service | 8019 | MMP_Notification | 通知中心 + 公告 + SignalR 推送 |
| bi-admin-service | 8020 | MMP_BI | BI 聚合统计 |

## 四、前端应用

| 应用 | 端口 | 类型 | 说明 |
|---|---|---|---|
| web-customer | 5173 | Vue 3 + Element Plus | C 端商城（浏览/购物车/下单） |
| web-merchant | 5174 | Vue 3 + Element Plus | 商户端（商品/订单/营销） |
| mobile-app | 5175 | uni-app Vue 3 | 移动端商城（H5） |
| web-admin | 5177 | Vue 3 + ECharts | 平台管理 BI 看板 |
| desktop-app | — | Electron 33 | 商户工作台（本地分发） |

> 前端统一：axios baseURL=`/api`，IIS 站点 URL Rewrite → 网关 8000；`/hub`（SignalR）同转发。

## 五、关键横切能力

| 能力 | 实现 | 位置 |
|---|---|---|
| **多租户** | X-Merchant-Id 头 + MultiTenantEntity + HasQueryFilter + Handler 显式过滤 | BuildingBlocks.MultiTenant |
| **CQRS/Mediator** | Mediator + Query/Command/Handler 分层 | BuildingBlocks.Core |
| **弹性调用** | Polly v8（重试/熔断/超时） | BuildingBlocks.Communication |
| **缓存** | ICacheService（Redis/In-Memory 切换）+ GetOrAddAsync 防击穿 | BuildingBlocks.Cache |
| **限流** | 网关 RateLimiter（并发 500 / 固定窗口 120·60s / 秒杀令牌桶 2000） | ApiGateway |
| **服务间安全** | X-Internal-Key 头校验 | 各服务 |
| **消息** | Outbox 模式 + 事件总线 + 幂等表 | messaging-service |
| **分布式锁** | Redis SETNX + TTL + Lua 释放 | BuildingBlocks.Cache |

## 六、数据架构

- **18 个业务库**（MMP_*），按服务垂直拆分（详见 docs/database/database-catalog.md）
- **增长特征**：订单/子单/明细（1:N:N）、日志、轨迹、秒杀记录为高速流水表
- **分库分表路线**：方案 A（SQL Server 表分区）为首选，数据量达阈值后按 `sharding-partition-templates.sql` 落地；读写分离 Phase 5 评估（详见 docs/reports/db-sharding-evaluation.md）
- **Redis 键规范**：
  - 秒杀：`seckill:stock:{id}` / `seckill:lock:{id}` / `seckill:active:list`
  - 商品：`product:public:detail:{id}` / `product:public:list:v{ver}:{page}:{size}` / `product:public:list:version`

## 七、部署拓扑（IIS）

- **25 个 IIS 站点**（21 后端 + 4 前端），每站点独立应用池（No Managed Code + AlwaysRunning）
- **依赖服务**：Redis（Windows 服务 redis，自启）+ SQL Server（MMP_* 库）
- **部署目录**：`E:\IISDeploy\services\` + `E:\IISDeploy\web\`
- **一键部署**：`scripts/create-iis-sites.ps1`（含 ARR 代理启用）
- **访问地址**：见 docs/guides/local-deployment.md 第三节

## 八、安全设计

- JWT：短名 role claim + MapInboundClaims=false；服务间 X-Internal-Key
- 多租户：请求级商户上下文 + 查询过滤器 + Handler 显式校验（三重防护）
- 涉密治理：appsettings.json 不入库，仅 Example 模板（占位符）；密钥已轮换
- 网关限流防刷：429 + Retry-After

## 九、扩展路线（二期）

| 阶段 | 内容 | 状态 |
|---|---|---|
| Week 19 | performance-service 全量压测 + 瓶颈优化 | 待做 |
| Week 20 | IIS 部署完成 / 一键启动脚本 / Aspire AppHost 补全 | ✅ 本期 |
| Week 21 | 监控告警 / 日志归档 / 链路追踪（OTel） | 待做 |
| Week 22 | 灰度发布 + 全量上线 | 待做 |

详见 `docs/reports/phase2-development-plan.md`。

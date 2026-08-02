# 项目上下文（会话恢复专用）

> **用途**：新会话开始时**整读本文件**即可恢复项目上下文（不必翻历史对话）。
> **维护**：每个阶段（服务）交付后必须同步更新本文件的「当前进度」与「下一步」。
> 版本对应：v5.6 · 2026-08-02 15:00 · 工作区已提交干净

---

## 一、项目概览

**多商户电商平台（MultiMerchantPlatform）** — 微服务架构，.NET 10 / C# 13 / SQL Server 2025 / Vue 3 前端。22 周路线图（Phase 0-5），当前 **Phase 2**。

- 服务间通信：YARP 网关（8000）+ `IServiceClient`（HTTP，可切 gRPC）+ 消息总线（messaging-service）
- 鉴权：JWT（identity-service 签发，`MapInboundClaims=false`，role 用短名）
- 多租户：商户维度 `X-Merchant-Id` 头 + `MultiTenantEntity` + `HasQueryFilter` + Handler 显式过滤
- 服务间内部调用：`X-Internal-Key`（MMP-Internal-Key-2026）

## 二、服务清单（11 服务 + 网关）

| 服务 | 端口 | 数据库 | 状态 | 说明 |
|---|---|---|---|---|
| identity-service | 8001 | MMP_Identity | ✅ | 注册/登录/JWT/失败锁定 |
| merchant-service | 8002 | MMP_Merchant | ✅ | 入驻/审核/店铺 + 内部查商户名 |
| product-service | 8003 | MMP_Product | ✅ | 分类/商品/SKU/上下架 + C 端公开接口 |
| order-service | 8004 | MMP_Order | ✅ | 跨商户拆单/状态机/库存联动 |
| pay-service | 8005 | MMP_Pay | ✅ | 支付单/模拟支付/退款/回调订单 |
| stock-service | 8006 | MMP_Stock | ✅ | 库存预占/扣减/释放 + 内部接口 |
| **cart-service** | 8007 | MMP_Cart | ✅ v5.5 | 购物车（买家隔离/同 SKU 合并） |
| **search-service** | 8008 | MMP_Search | ✅ v5.5 | 商品搜索索引（在售/关键词/价格） |
| promotion-service | 8009 | MMP_Promotion | ⏳ **下一个** | 优惠券/满减/活动 |
| messaging-service | 8010 | MMP_Infra | ✅ | 消息总线（Outbox/通配订阅） |
| logging-service | 8011 | MMP_Infra | ✅ | 日志批量上报/查询/统计 |
| email-service | 8015 | MMP_Email | ✅ | 邮件（MailKit/DryRun/模板/重试） |
| ApiGateway（YARP） | 8000 | — | ✅ | 路由转发 |

前端：`src/apps/web-customer`（Vue 3.5 + Vite 8 + Element Plus，C 端商城，端口 5173 dev）

## 三、当前进度

- **Phase 2 Week 10 已完成**：cart-service + search-service（提交 c5512d7）
- **Phase 1 全部完成**（v4.7-v5.4）：identity → merchant → product → order → pay → stock → 库存联动 → C 端 Web 商城
- 全量编译 **0 警告 0 错误**（22 项目）；最近提交 `225deab`（v5.6 规范）

## 四、下一步（按 PROJECT_PLAN.md 路线图）

| 周次 | 任务 | 端口/说明 |
|---|---|---|
| **10-11** | **promotion-service** | 8009，优惠券/满减/活动 |
| 10-11 | review-service | 评价 |
| 11 | logistics-service + settlement-service | 物流 + 结算 |
| 12 | im-service | 即时通讯 |
| 12-13 | 商户端 Web 前端（web-merchant） | Vue 3 |
| 13 | 移动端 uni-app 骨架 | App 可运行 |

## 五、工作流程约定（强制）

1. **阶段交付**：阶段完成 → 编译 0 警告 0 错误 + 冒烟测试 → 提交 Git（commit+push）→ 再进下一阶段
2. **会话边界**：每完成一个服务（编译+冒烟+文档+提交）→ 开新会话继续；新会话第一步**整读本文件**恢复上下文
3. **文档分类**：`modules/`（模块）、`reports/`（报告）、`architecture/`（架构+规范）、`database/`（库表）、`guides/`（指南）；PROJECT_PLAN/CHANGELOG/DOC_INDEX/CONTEXT 留根目录
4. **API 注解**：服务开 GenerateDocumentationFile + IncludeXmlComments，0 警告交付
5. **分层**：Mediator + CQRS、充血实体（private set + 领域方法）、多租户三重防护、X-Internal-Key 内部调用

## 六、已踩坑清单（避免重犯）

- 多个命名 HttpClient 注册同一服务类型互相覆盖 → 客户端注入 `IHttpClientFactory` 按名 `CreateClient`
- 网关：Controller 自带 `api/xxx` 前缀的服务（cart/search）路由**不做前缀剥离**
- JWT role 用长 URI 名致角色授权失效 → 签发短名 "role" + `MapInboundClaims=false`
- Swashbuckle 需 10.1.7+；Microsoft.OpenApi 2.0 类型在 `Microsoft.OpenApi` 命名空间（非 Models）
- 服务 Release 直跑端口用 `ASPNETCORE_URLS` 显式指定
- EF 关系修复致 `TryComplete` 误判 → 必须 Include 全部子单再判断
- 测试/演示后停服务进程（PowerShell `Get-NetTCPConnection -LocalPort` 批量停）

## 七、关键文档索引

| 文档 | 路径 |
|---|---|
| 路线图 | `docs/PROJECT_PLAN.md`（v4.1，22 周） |
| 编码规范 | `docs/architecture/coding-standards.md`（v1.0） |
| 变更记录 | `docs/CHANGELOG.md`（当前 v5.6） |
| 文档索引 | `docs/DOC_INDEX.md` |
| Token 分析 | `docs/reports/token-usage-analysis.md` |
| 模块文档 | `docs/modules/<service>.md` × 11 |

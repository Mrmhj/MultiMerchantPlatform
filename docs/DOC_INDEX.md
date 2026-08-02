# 文档索引

> 本文件汇总所有项目文档的路径和状态。新增文档时更新此索引。

## 主文档

| 文档 | 路径 | 状态 | 说明 |
|------|------|------|------|
| 项目方案 | `E:\MultiMerchantPlatform\docs\PROJECT_PLAN.md` | ✅ v4.1 | 总体规划，每次方案调整更新 |
| 项目上下文 | `E:\MultiMerchantPlatform\docs\CONTEXT.md` | ✅ v5.8 | 会话恢复专用，每阶段更新（新会话整读即恢复） |
| 架构设计 | `E:\MultiMerchantPlatform\docs\architecture\ARCHITECTURE.md` | 📝 待编写 | 技术架构详细设计 |
| 编码规范 | `E:\MultiMerchantPlatform\docs\architecture\coding-standards.md` | ✅ v1.0 | 全项目强制规范（含 Phase 1 业务开发规范） |
| API 规范 | `E:\MultiMerchantPlatform\docs\API_SPEC.md` | 📝 待编写 | 所有微服务 API 接口文档 |
| 数据库设计 | `E:\MultiMerchantPlatform\docs\DATABASE.md` | 📝 待编写 | 所有数据库表结构设计 |
| 部署指南 | `E:\MultiMerchantPlatform\docs\DEPLOYMENT.md` | 📝 待编写 | 部署与运维操作指南 |
| 变更记录 | `E:\MultiMerchantPlatform\docs\CHANGELOG.md` | ✅ v6.0 | 每次调整追加记录 |
| 文档索引 | `E:\MultiMerchantPlatform\docs\DOC_INDEX.md` | ✅ 本文档 | 文档路径汇总 |

## 模块文档

### P0 核心服务

| 模块 | 路径 | 状态 | 优先级 |
|------|------|------|--------|
| identity-service | `docs/modules/identity-service.md` | ✅ 已完成 (v4.7) | P0 (Phase 1 Week 4) |
| merchant-service | `docs/modules/merchant-service.md` | ✅ 已完成 (v4.8) | P0 (Phase 1 Week 4-5) |
| product-service | `docs/modules/product-service.md` | ✅ 已完成 (v4.9) | P0 (Phase 1 Week 5-6) |
| order-service | `docs/modules/order-service.md` | ✅ 已完成 (v5.0) | P0 (Phase 1 Week 6-7) |
| pay-service | `docs/modules/pay-service.md` | ✅ 已完成 (v5.1) | P0 (Phase 1 Week 7-8) |
| stock-service | `docs/modules/stock-service.md` | ✅ 已完成 (v5.2) | P0 (Phase 1 Week 8) |
| messaging-service | `docs/modules/messaging-service.md` | ✅ 已完成 (v4.2) | P0 |
| logging-service | `docs/modules/logging-service.md` | ✅ 已完成 (v4.3) | P0 |
| email-service | `docs/modules/email-service.md` | ✅ 已完成 (v4.4) | P0 (Phase 0 Week 3) |

### P1 支撑服务

| 模块 | 路径 | 状态 | 优先级 |
|------|------|------|--------|
| cart-service | `docs/modules/cart-service.md` | ✅ 已完成 (v5.5) | P1 (Phase 2 Week 10) |
| search-service | `docs/modules/search-service.md` | ✅ 已完成 (v5.5) | P1 (Phase 2 Week 10) |
| promotion-service | `docs/modules/promotion-service.md` | ✅ 已完成 (v5.7) | P1 (Phase 2 Week 10-11) |
| review-service | `docs/modules/review-service.md` | ✅ 已完成 (v5.8) | P1 (Phase 2 Week 10-11) |
| logistics-service | `docs/modules/logistics-service.md` | ✅ 已完成 (v5.9) | P1 (Phase 2 Week 11) |
| settlement-service | `docs/modules/settlement-service.md` | ✅ 已完成 (v5.9) | P1 (Phase 2 Week 11) |
| im-service | `docs/modules/im-service.md` | ✅ 已完成 (v6.0) | P1 (Phase 2 Week 12) |

### P2 平台服务

| 模块 | 路径 | 状态 | 优先级 |
|------|------|------|--------|
| performance-service | `docs/modules/performance-service.md` | 📝 待编写 | P2 (v4新增) |
| risk-service | `docs/modules/risk-service.md` | 📝 待编写 | P2 |
| notification-service | `docs/modules/notification-service.md` | 📝 待编写 | P2 |

### P3 分析平台

| 模块 | 路径 | 状态 | 优先级 |
|------|------|------|--------|
| bi-admin | `docs/modules/bi-admin.md` | 📝 待编写 | P3 |

### 前端应用

| 模块 | 路径 | 状态 | 说明 |
|------|------|------|------|
| web-customer | `docs/modules/web-customer.md` | ✅ 已完成 (v5.4) | C端 Web (Vue 3 + Element Plus) |
| web-merchant | `docs/modules/web-merchant.md` | 📝 待编写 | 商户端 Web (Vue 3 + Element Plus) |
| web-admin | `docs/modules/web-admin.md` | 📝 待编写 | 管理后台 (Vue 3 + Element Plus) |
| mobile-app | `docs/modules/mobile-app.md` | 📝 待编写 | 移动端 uni-app (v4.1改) |
| desktop-app | `docs/modules/desktop-app.md` | 📝 待编写 | 桌面端 Electron (v4.1改) |

### 报告

| 类型 | 路径 | 说明 |
|------|------|------|
| 压测报告 | `docs/reports/loadtest-*.html` | performance-service 自动生成 |
| 分析报告 | `docs/reports/analysis-*.html` | BI 平台导出 |

---

## 文档状态说明

- ✅ 已完成 — 内容完整，已审核
- 📝 待编写 — 已在方案中规划，尚未编写详细文档
- 🔄 更新中 — 正在修改
- ❌ 已废弃 — 不再使用

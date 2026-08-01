# 文档索引

> 本文件汇总所有项目文档的路径和状态。新增文档时更新此索引。

## 主文档

| 文档 | 路径 | 状态 | 说明 |
|------|------|------|------|
| 项目方案 | `E:\MultiMerchantPlatform\docs\PROJECT_PLAN.md` | ✅ v4.1 | 总体规划，每次方案调整更新 |
| 架构设计 | `E:\MultiMerchantPlatform\docs\ARCHITECTURE.md` | 📝 待编写 | 技术架构详细设计 |
| API 规范 | `E:\MultiMerchantPlatform\docs\API_SPEC.md` | 📝 待编写 | 所有微服务 API 接口文档 |
| 数据库设计 | `E:\MultiMerchantPlatform\docs\DATABASE.md` | 📝 待编写 | 所有数据库表结构设计 |
| 部署指南 | `E:\MultiMerchantPlatform\docs\DEPLOYMENT.md` | 📝 待编写 | 部署与运维操作指南 |
| 变更记录 | `E:\MultiMerchantPlatform\docs\CHANGELOG.md` | ✅ v4.1 | 每次调整追加记录 |
| 文档索引 | `E:\MultiMerchantPlatform\docs\DOC_INDEX.md` | ✅ 本文档 | 文档路径汇总 |

## 模块文档

### P0 核心服务

| 模块 | 路径 | 状态 | 优先级 |
|------|------|------|--------|
| identity-service | `docs/modules/identity-service.md` | 📝 待编写 | P0 |
| merchant-service | `docs/modules/merchant-service.md` | 📝 待编写 | P0 |
| product-service | `docs/modules/product-service.md` | 📝 待编写 | P0 |
| order-service | `docs/modules/order-service.md` | 📝 待编写 | P0 |
| pay-service | `docs/modules/pay-service.md` | 📝 待编写 | P0 |
| stock-service | `docs/modules/stock-service.md` | 📝 待编写 | P0 |
| messaging-service | `docs/modules/messaging-service.md` | 📝 待编写 | P0 |
| logging-service | `docs/modules/logging-service.md` | 📝 待编写 | P0 |

### P1 支撑服务

| 模块 | 路径 | 状态 | 优先级 |
|------|------|------|--------|
| cart-service | `docs/modules/cart-service.md` | 📝 待编写 | P1 |
| search-service | `docs/modules/search-service.md` | 📝 待编写 | P1 |
| promotion-service | `docs/modules/promotion-service.md` | 📝 待编写 | P1 |
| review-service | `docs/modules/review-service.md` | 📝 待编写 | P1 |
| logistics-service | `docs/modules/logistics-service.md` | 📝 待编写 | P1 |
| settlement-service | `docs/modules/settlement-service.md` | 📝 待编写 | P1 |
| email-service | `docs/modules/email-service.md` | 📝 待编写 | P1 (v4新增) |

### P2 平台服务

| 模块 | 路径 | 状态 | 优先级 |
|------|------|------|--------|
| im-service | `docs/modules/im-service.md` | 📝 待编写 | P2 (v4新增) |
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
| web-customer | `docs/modules/web-customer.md` | 📝 待编写 | C端 Web (Vue 3 + Element Plus) |
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

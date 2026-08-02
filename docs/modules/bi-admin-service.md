# bi-admin-service 模块文档

> **文档路径**：`docs/modules/bi-admin-service.md`
> **版本**：v7.0 · 2026-08-02 · **端口 8020 · 数据库 MMP_BI（独立库）**
> **定位**：P3 平台支撑服务 — BI 分析平台（跨服务聚合统计 + 看板数据源）

---

## 一、职责概述

| 能力 | 说明 |
|------|------|
| **跨服务取数** | 通过内部接口（X-Internal-Key）从 order / merchant / product / identity 服务拉取统计口径数据 |
| **聚合落库** | 同步时将上游明细聚合为按天销售 / 商户排行 / 商品排行 / 订单状态分布 / 总览快照五类聚合表（MMP_BI） |
| **看板 API** | 平台管理员（admin）查询：核心指标总览、销售趋势、商户排行、商品排行、订单状态分布 |
| **手动同步** | `POST /api/bi/sync` 一键重建聚合表（幂等：整体覆盖），供前端「同步数据」按钮触发 |

---

## 二、技术架构

```
┌────────────────────────────────────────────────────────────┐
│                    bi-admin-service (8020)                  │
│                                                            │
│  ┌───────────────┐        ┌──────────────────────────────┐ │
│  │ BiController  │        │ BiSyncService（同步，整体覆盖） │ │
│  │ (admin 角色)   │ ────►  │ 拉取上游 → 清表 → 重建聚合表    │ │
│  └───────────────┘        └──────────────┬───────────────┘ │
│        │ 查询（CQRS Handler 读聚合表）       │ 依赖             │
│        ▼                                  ▼                │
│  ┌───────────────┐        ┌──────────────────────────────┐ │
│  │ BiDbContext   │        │ BiDataClients（命名 HttpClient│ │
│  │ MMP_BI 聚合表  │        │  携带 X-Internal-Key 默认头）   │ │
│  └───────────────┘        └──────────────────────────────┘ │
│                                                            │
│  上游：order 8004 /api/orders/internal/bi-stats             │
│        merchant 8002 /api/merchants/internal/stats          │
│        product 8003 /api/products/internal/stats            │
│        identity 8001 /api/users/internal/stats              │
└────────────────────────────────────────────────────────────┘
```

### 分层（Mediator + CQRS 强制）

```
Controller → IMediator → IQueryHandler → BiDbContext（聚合表）
          → BiSyncService（命令式同步，非 CQRS 命令，直接服务调用）
```

- 写操作：`BiSyncService.SyncAsync`（唯一写路径，事务内整体覆盖）
- 读操作：`BiOverviewQuery` / `BiSalesTrendQuery` / `BiMerchantRankQuery` / `BiProductRankQuery` / `BiOrderStatusQuery`
- 客户端：`BiDataClients`（Scoped）— 按名 `CreateClient` 取命名 HttpClient（order/merchant/product/identity），避免注册覆盖

---

## 三、数据库设计（MMP_BI 库）

| 表 | 说明 | 关键字段 |
|----|------|---------|
| `BiOverviews` | 总览快照（单行） | TotalGmv(18,2) / TotalOrders / PaidOrders / CompletedOrders / MerchantCount / ProductCount / UserCount / SyncedAt |
| `BiDailySales` | 按天销售聚合 | Date(UTC) / Gmv(18,2) / OrderCount；索引 Date |
| `BiMerchantSales` | 商户销售排行 | MerchantId / MerchantName(100) / Gmv(18,2) / OrderCount；索引 (MerchantId,Gmv) |
| `BiProductSales` | 商品销售排行 | ProductId / ProductName(200) / Quantity / Amount(18,2)；索引 (ProductId,Amount) |
| `BiOrderStatusDist` | 主订单状态分布 | Status(1待付款 2已付款 3已完成 4已取消) / Count；索引 Status |

> 聚合表为**只读副本 + 聚合结果**（非业务明细），同步时整体重建（`ExecuteDelete` + 批量 AddRange），天然幂等。

---

## 四、API 一览（全部 admin 角色，网关 `/api/bi/**`）

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/bi/overview` | 核心指标（GMV/订单/商户/商品/用户 + 同步时间） |
| GET | `/api/bi/sales-trend?days=30` | 销售趋势（按天 GMV + 订单数，days 1-90 钳制） |
| GET | `/api/bi/merchant-rank?top=10` | 商户销售排行（GMV 降序，top 1-50 钳制） |
| GET | `/api/bi/product-rank?top=10` | 商品销售排行（销售额降序，top 1-50 钳制） |
| GET | `/api/bi/order-status` | 订单状态分布（饼图数据） |
| POST | `/api/bi/sync` | 手动触发同步（502 — 上游取数失败） |
| GET | `/api/health` | 健康检查（数据库连通性） |

---

## 五、上游内部接口约定（X-Internal-Key）

| 服务 | 接口 | 返回 |
|------|------|------|
| order 8004 | `GET /api/orders/internal/bi-stats?start&end` | TotalGmv / TotalOrderCount / PaidOrderCount / CompletedOrderCount / DailySales / MerchantRank / ProductRank / OrderStatus |
| merchant 8002 | `GET /api/merchants/internal/stats` | total / approved / pending |
| product 8003 | `GET /api/products/internal/stats` | total / onSale |
| identity 8001 | `GET /api/users/internal/stats` | total |

> **销售口径**：子订单状态 ∈ {Paid, Shipped, Completed}（已付款即计入 GMV）；按天子订单取创建日（UTC）。

---

## 六、前端配套

- **web-admin 平台管理后台**（`src/apps/web-admin`，端口 5177）：Vue 3.5 + Vite 8 + TS + Element Plus + **ECharts 5**
  - 登录（admin）→ BI 看板：指标卡 × 6 + 销售趋势双轴折线 + 商户/商品排行条形图 + 订单状态饼图
  - 「同步数据」按钮 → `POST /api/bi/sync` → 成功后刷新全部图表；支持 7/30/90 天趋势切换
  - 详见 `docs/modules/web-admin.md`

---

## 七、冒烟覆盖（tests/smoke-bi.sh）

注册提权 → 健康 → 鉴权拦截（无 token 401 / 买家 403）→ 造数（取在售商品 → 补库存 → 下单 → 模拟支付）→ 同步（success + 各计数字段）→ 五类看板接口字段断言 → 参数钳制（days=999 / top=999 仍 200）

**结果：31/31 通过（v7.0）**

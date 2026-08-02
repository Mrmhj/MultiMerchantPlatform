# settlement-service 结算服务

> 模块文档 · 摩登时代 · 2026-08-02 · Phase 2 Week 11

## 一、概述

| 项 | 值 |
|---|---|
| 端口 | **8014** |
| 数据库 | `MMP_Settlement`（Settlements / SettlementItems / CommissionRules） |
| 网关路由 | `/api/settlements/**`、`/api/commission-rules/**`（直通，无前缀剥离） |
| 商户端 | 结算单列表 / 详情 / 概览 / 佣金比例，JWT + `X-Merchant-Id` 头 |
| 平台端 | 生成结算单 / 确认结算 / 打款 / 佣金规则管理（admin） |
| 上游依赖 | order-service 内部接口（已完成子订单），X-Internal-Key |

**定位**：结算域——平台按周期扫描**已完成子订单**生成商户结算单，按佣金规则计算平台佣金，商户可查看结算进度与明细，平台确认结算并打款。

## 二、核心设计

### 实体模型

```
CommissionRule（多租户：MerchantId 唯一，一商户一条规则）
 └─ Rate（佣金比例 0-100 百分数，如 10 = 10%）

Settlement（多租户：MerchantId，聚合根）
 ├─ 周期：CycleStart / CycleEnd
 ├─ 金额：TotalOrderAmount / TotalCommission / SettlementAmount（= 总额 - 佣金，计算属性）
 ├─ 状态机：Pending（待结算）→ Settled（已结算）→ Paid（已打款）
 └─ 明细：SettlementItems（SubOrderId 唯一，一子单仅结算一次）

SettlementItem
 ├─ SubOrderId（唯一索引，防重复结算）/ OrderNo（快照）
 └─ ProductAmount / CommissionAmount / SettleAmount（= 商品金额 - 佣金）
```

### 关键规则

1. **数据源**：生成结算单时通过 order-service 内部接口 `GET /api/orders/internal/completed-suborders`（X-Internal-Key）拉取**已完成**子订单（含 OrderNo/商户/金额/完成时间，完成时间 = 子订单 `UpdatedAt`，Complete 时写入）
2. **佣金计算**：`Commission = ProductAmount × Rate%`（四舍五入 2 位）；无佣金规则商户使用平台默认 `DefaultCommissionRate`（默认 5，appsettings 可配）
3. **幂等防重**：`SettlementItem.SubOrderId` 唯一索引 → 同一子订单只结算一次；重复生成自动跳过已结算子订单（`skippedCount` 反馈），实测重复生成 skipped=2
4. **多租户三重防护**（商户维度）：`MultiTenantEntity` + HasQueryFilter + Handler 显式过滤；缺 `X-Merchant-Id` → 400 `MERCHANT_REQUIRED`
5. **状态机**：`Settle`（Pending→Settled，无明细不可确认）→ `MarkPaid`（Settled→Paid），越权流转 → 400 `SETTLEMENT_STATE_INVALID`
6. **商户概览**：按状态聚合（待结算单数/金额、已结算+已打款累计、累计佣金）

## 三、API 清单

### 平台端（admin）

| 方法 | 路径 | 说明 |
|---|---|---|
| POST | `/api/settlements/generate` | 生成结算单（body 可选周期，拉已完成子订单 → 按商户聚合 + 佣金计算，幂等） |
| GET | `/api/settlements` | 结算单列表（status / merchantId 过滤 + 分页） |
| POST | `/api/settlements/{id}/settle` | 确认结算（Pending → Settled） |
| POST | `/api/settlements/{id}/paid` | 标记已打款（Settled → Paid） |
| PUT | `/api/commission-rules/{merchantId}` | 设置 / 更新佣金比例（0-100，一商户一条） |
| GET | `/api/commission-rules` | 佣金规则列表（分页） |

### 商户端（JWT + X-Merchant-Id）

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/settlements/merchant` | 我的结算单列表（status 过滤 + 分页） |
| GET | `/api/settlements/merchant/{id}` | 结算单详情（含明细） |
| GET | `/api/settlements/merchant/summary` | 结算概览（待结算/已结算金额与单数） |
| GET | `/api/settlements/merchant/commission` | 我的佣金比例（未配置返回平台默认 + `isDefault=true`） |

## 四、状态与约束

- 结算状态：1 待结算 / 2 已结算 / 3 已打款
- 佣金比例 0-100（越界 → 400 `INVALID_COMMISSION_RATE`）
- 结算单无明细不可确认（400 `SETTLEMENT_EMPTY`）；结算单不存在 → 404；非 admin 调平台接口 → 403

## 五、联调验证（2026-08-02 实测）

```
健康检查 ✅ → 设置佣金规则 10% ✅ → 生成结算单：
  已完成子订单 2 条（52.30 + 10.00 = 62.30）→ 佣金 6.23（10%）→ 结算 56.07 ✅（明细逐条正确）
重复生成幂等（skipped=2，无新结算单）✅ → 确认结算（Pending→Settled）✅
打款（Settled→Paid）✅ → 商户列表 ✅ → 商户概览（paidCount=1 / 已结算 56.07 / 佣金 6.23）✅
商户佣金比例（10%）✅ → 缺商户头 400 MERCHANT_REQUIRED ✅
网关转发：/api/settlements/** → 8014 ✅
```

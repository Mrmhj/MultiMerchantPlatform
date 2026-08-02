# promotion-service 促销服务

> 模块文档 · 摩登时代 · 2026-08-02 · Phase 2 Week 10-11

## 一、概述

| 项 | 值 |
|---|---|
| 端口 | **8009** |
| 数据库 | `MMP_Promotion`（Coupons / UserCoupons / PromotionActivities 三表） |
| 网关路由 | `/api/promotion/**`（直通，无前缀剥离） |
| 商户端 | 优惠券/满减活动管理，需 JWT + `X-Merchant-Id` 头 |
| 买家端 | 领券/我的券，JWT 鉴权；可领列表/进行中活动公开 |
| 内部接口 | X-Internal-Key 校验（order-service 核销用） |

**定位**：营销促销域——商户发优惠券（满减）、买家领券使用、商户建满减活动，为订单侧提供优惠券核销扩展点（内部接口）。

## 二、核心设计

### 实体模型（三表）

```
Coupon（优惠券模板，商户维度 MultiTenantEntity）
 ├─ 规则：满 ThresholdAmount 减 DiscountAmount（满减券）
 ├─ 库存：TotalQuantity（0=不限量）/ ClaimedCount / LimitPerUser（1-99）
 ├─ 有效期：ValidFrom ~ ValidUntil（领取与使用共用窗口）
 └─ 状态：Active / Inactive（停用后不可再领，已领不受影响）

UserCoupon（用户券，买家维度 UserId 隔离）
 ├─ 快照：券名/规则/有效期（领取时复制，模板改动不影响已领券）
 ├─ 状态机：Unused → Used（核销后不可回退；Expired 查询时按有效期推导）
 └─ 冗余：MerchantId（便于商户侧对账与后续订单校验）

PromotionActivity（满减活动，商户维度 MultiTenantEntity）
 └─ 状态机：Draft ⇄ Active → Ended（Ended 由时间窗口自动推导收尾）
```

### 关键规则

1. **领券三重校验**：模板启用 + 有效期窗口内 + 总量未领完（`ClaimOne` 内聚），另在 Handler 校验每人限领（超限 `LIMIT_REACHED`）
2. **多租户三重防护**（商户维度）：`MultiTenantEntity` + DbContext `HasQueryFilter` + Handler 显式 `Where(MerchantId)`；缺 `X-Merchant-Id` 头 → 400 `MERCHANT_REQUIRED`（跨商户实测隔离）
3. **买家隔离**：UserCoupon 全部查询按 `UserId` 过滤，他用户不可见/不可核销他人券
4. **核销幂等**：`MarkUsed` 对已核销券直接返回成功（内部接口 `UseCouponResult.Success=true`），重复回调不报错
5. **活动到期推导**：Active 且已过结束时间 → 查询时自动收尾为 Ended（`EndIfExpired`），无需定时任务
6. **并发注记**：领券计数非原子（开发阶段可接受）；生产可改 SQL 原子自增 + 检查约束防超发

## 三、API 清单

### 商户端（JWT + X-Merchant-Id）

| 方法 | 路径 | 说明 |
|---|---|---|
| POST | `/api/promotion/coupons` | 创建优惠券（满减，限量/限领/有效期） |
| GET | `/api/promotion/coupons` | 券列表（分页，page/pageSize） |
| GET | `/api/promotion/coupons/{id}` | 券详情（含 IsClaimable） |
| PUT | `/api/promotion/coupons/{id}/status` | 启用/停用（body: active） |
| POST | `/api/promotion/activities` | 创建满减活动（初始 Draft） |
| GET | `/api/promotion/activities` | 活动列表（分页，status: all/draft/active/ended） |
| GET | `/api/promotion/activities/{id}` | 活动详情 |
| PUT | `/api/promotion/activities/{id}/status` | 启用/停用 |

### 买家端 / 公开

| 方法 | 路径 | 说明 | 鉴权 |
|---|---|---|---|
| GET | `/api/promotion/coupons/available` | 可领券列表（启用+有效期内+未领完） | 公开 |
| POST | `/api/promotion/coupons/{id}/claim` | 领取优惠券 | JWT |
| GET | `/api/promotion/my/coupons` | 我的券（status: all/unused/used/expired） | JWT |
| GET | `/api/promotion/activities/active` | 进行中满减活动 | 公开 |

### 内部接口（X-Internal-Key，供 order-service）

| 方法 | 路径 | 说明 |
|---|---|---|
| POST | `/api/promotion/internal/coupons/use` | 核销用户券（success/error + 优惠金额），支付确认时调用 |

## 四、状态与约束

- 金额规则：优惠金额必须 > 0 且不大于门槛（`DISCOUNT_EXCEEDS_THRESHOLD`）
- 领券失败场景：未启用/未到有效期/已领完 → `COUPON_NOT_CLAIMABLE`；达限领数 → `LIMIT_REACHED`
- 核销失败场景：券不存在 → `COUPON_NOT_FOUND`；过期 → `COUPON_EXPIRED`；未到可用时间 → `COUPON_NOT_STARTED`
- 活动已过结束时间不可启用 → `ACTIVITY_ENDED`
- 内部密钥错误 → 401；未登录（需鉴权接口）→ 401；缺商户头 → 400

## 五、联调验证（2026-08-02 实测）

```
健康检查 ✅ → 建券（满100减20）✅ → 缺商户头 400 ✅ → 券列表分页 ✅
可领列表（公开）✅ → 领券 ✅ → 重复领券 LIMIT_REACHED ✅ → 我的券 unused ✅
内部核销：错误密钥 401 ✅ → 正确密钥成功（减20）✅ → 重复核销幂等 ✅
我的券 used 过滤 ✅ → 建活动（Draft）✅ → 启用（Active）✅ → 进行中（公开）✅
停用（Draft）✅ → 进行中为空 ✅ → 网关转发 ✅ → 跨商户隔离（商户B空列表）✅
```

## 六、已知限制与扩展

- **订单联动未接线**：内部核销接口已就绪，order-service 下单选券/支付核销留待后续阶段统一接线（仿库存 reserve/confirm 模式）
- **券类型单一**：当前仅满减券（`CouponType.FullReduction`）；折扣券/包邮券为枚举扩展点
- **并发领券**：计数非原子（SQL 原子自增为生产升级项）
- **到期任务**：活动 Ended 为查询时惰性推导，无后台任务；量大会话可加定时收尾
- **门槛校验**：核销接口未校验订单金额是否达门槛（`ThresholdAmount` 由 order-service 下单时自行校验，接口已返回快照值）

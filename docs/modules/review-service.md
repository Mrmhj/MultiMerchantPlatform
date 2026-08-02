# review-service 评价服务

> 模块文档 · 摩登时代 · 2026-08-02 · Phase 2 Week 10-11

## 一、概述

| 项 | 值 |
|---|---|
| 端口 | **8012** |
| 数据库 | `MMP_Review`（Reviews 表） |
| 网关路由 | `/api/reviews/**`（直通，无前缀剥离） |
| 买家端 | 创建评价 / 我的评价，JWT 鉴权 |
| 商户端 | 评价列表 / 回复 / 隐藏，JWT + `X-Merchant-Id` 头 |
| 公开接口 | 商品评价列表（仅可见）+ 评分统计，无需鉴权 |

**定位**：商品评价域——买家对订单商品评分评价，商户回复/管理，C 端商品详情页展示评分与评价。

## 二、核心设计

### 实体模型（单表 Review）

```
Review
 ├─ 归属：UserId（买家评价人）+ MerchantId（商户维度多租户）
 ├─ 订单关联：OrderId / SubOrderId（同一子订单项仅一条评价，唯一索引）
 ├─ 商品快照：ProductId / ProductName / SkuId / SkuSpec
 ├─ 评价内容：Rating（1-5）/ Content（1-500 字）/ IsAnonymous
 ├─ 商户管理：ReplyContent / RepliedAt（可修改回复）
 └─ 状态：Visible / Hidden（隐藏后 C 端不可见、不计入公开统计）
```

### 关键规则

1. **防重复评价**：唯一索引 `(UserId, SubOrderId, ProductId)` + Handler 前置校验 → 同一订单商品只能评一次（`REVIEW_ALREADY_EXISTS`）
2. **多租户三重防护**（商户维度）：`MultiTenantEntity` + DbContext `HasQueryFilter` + Handler/Controller 显式过滤；缺 `X-Merchant-Id` → 400 `MERCHANT_REQUIRED`（跨商户实测隔离）
3. **买家隔离**：我的评价/创建评价按 JWT `UserId` 过滤，他用户不可见/不可操作他人评价
4. **评分统计口径**：公开接口 `averageRating` / `ratingDistribution` 仅统计**可见**评价（隐藏后自动剔除，实测 3星隐藏后平均分 5.0）
5. **列表过滤**：公开接口支持 `rating` 评分过滤，`totalCount` 为过滤后条数（曾误用全量计数，已修复）；商户接口支持 `productId/rating/status` 组合过滤
6. **匿名展示**：匿名评价 C 端 `displayName` 显示「匿名用户」
7. **创建校验**：`[Range(1,5)]` 评分等由 ModelState 在绑定层拦截（400）；领域层再兜底校验

## 三、API 清单

### 买家端（JWT）

| 方法 | 路径 | 说明 |
|---|---|---|
| POST | `/api/reviews` | 创建评价（订单商品，防重复） |
| GET | `/api/reviews/my` | 我的评价（分页） |

### 商户端（JWT + X-Merchant-Id）

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/reviews/merchant` | 评价列表（productId/rating/status 过滤 + 分页） |
| PUT | `/api/reviews/{id}/reply` | 回复评价（可修改，覆盖式） |
| PUT | `/api/reviews/{id}/status` | 隐藏 / 恢复可见（body: visible） |

### 公开（无需鉴权）

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/reviews/product/{productId}` | 商品评价列表（仅可见 + 平均分 + 评分分布 + 分页，支持 rating 过滤） |

## 四、状态与约束

- 评分 1-5（越界 → ModelState 400 `Rating must be between 1 and 5`）
- 内容 1-500 字、回复 1-500 字（领域层校验 `INVALID_CONTENT` / `INVALID_REPLY`）
- 重复评价 → 400 `REVIEW_ALREADY_EXISTS`；评价不存在或非本商户 → 404
- 未登录（需鉴权接口）→ 401；缺商户头 → 400

## 五、联调验证（2026-08-02 实测）

```
登录 ✅ → 创建评价（5星）✅ → 同订单重复评价拦截 ✅ → 无效评分 400 ✅
匿名 3 星评价 ✅ → 我的评价（2条）✅ → 缺商户头 400 ✅ → 商户列表（2条）✅
跨商户隔离（商户B 空）✅ → 公开统计（平均4/共2/5星×1）✅ → 评分过滤（rating=5 → 1条）✅
商户回复 ✅ → 隐藏 3 星 → 公开仅 1 条 + 平均 5.0 ✅ → hidden 过滤 ✅ → 恢复可见 ✅
网关转发 ✅ → 匿名展示「匿名用户」✅
```

## 六、已知限制与扩展

- **未校验订单真实性**：创建评价未调用 order-service 校验订单归属（Phase 2 简化，前端传 OrderId/SubOrderId；后续可加内部接口校验防刷评）
- **匿名策略简化**：当前仅「匿名用户」占位，未接 identity 查真实昵称（非匿名评价 displayName 由前端展示用户名）
- **图片/追评**：未支持评价图片与追加评价（Phase 3 扩展项）
- **审核流**：评价默认直接可见，未做平台先审后发（商户可隐藏兜底）
- **评分统计缓存**：实时聚合，评价量大时可加商品维度评分缓存（search-service 索引可联动）

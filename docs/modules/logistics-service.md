# logistics-service 物流服务

> 模块文档 · 摩登时代 · 2026-08-02 · Phase 2 Week 11

## 一、概述

| 项 | 值 |
|---|---|
| 端口 | **8013** |
| 数据库 | `MMP_Logistics`（LogisticsCompanies / Shipments / ShipmentTracks） |
| 网关路由 | `/api/logistics/**`（直通，无前缀剥离） |
| 买家端 | 按子订单查我的物流轨迹，JWT 鉴权 |
| 商户端 | 运单列表 / 详情 / 启用物流公司，JWT + `X-Merchant-Id` 头 |
| 平台端 | 物流公司管理（admin） |
| 内部接口 | 创建运单 / 轨迹推进（`X-Internal-Key`） |

**定位**：物流域——商户发货后自动生成运单（订单-物流联动），物流轨迹模拟推进（演示环境无真实物流 API，通过内部接口模拟物流公司回调），买家/商户随时查询。

## 二、核心设计

### 实体模型

```
LogisticsCompany（平台级，非多租户）
 ├─ Code（唯一编码，如 SF/YTO/ZTO）/ Name / TrackingUrlTemplate（{no} 占位）
 └─ IsEnabled（停用后商户不可选）

Shipment（多租户：MerchantId + BuyerUserId 买家隔离）
 ├─ 订单关联：SubOrderId（唯一，一子单一运单）/ OrderId / OrderNo（快照）
 ├─ 物流信息：CarrierCode / CarrierName（快照）/ TrackingNo（唯一）
 ├─ 状态机：Created → InTransit → OutForDelivery → Signed，任意状态可转 Exception
 └─ 轨迹：ShipmentTracks（ShipmentId + Status 快照 + Description + Location + TrackedAt）
```

### 关键规则

1. **订单-物流联动**：商户发货（order-service `POST /api/orders/merchant/{id}/ship`）携带 `carrierCode + trackingNo` → order-service 发货成功后回调 logistics-service 内部接口**自动创建运单**（物流服务不可用不阻断发货，仅记日志）；运单公司名按编码从 LogisticsCompanies 自动带出快照
2. **运单唯一性**：`SubOrderId` 唯一（一子订单一运单）+ `TrackingNo` 唯一，重复创建 → 400 `SHIPMENT_ALREADY_EXISTS` / `TRACKING_NO_EXISTS`
3. **多租户三重防护**（商户维度）：`MultiTenantEntity` + DbContext `HasQueryFilter` + Handler/Controller 显式过滤；缺 `X-Merchant-Id` → 400 `MERCHANT_REQUIRED`
4. **买家隔离**：运单归属 `BuyerUserId`（订单买家），买家仅可查自己的子订单运单，他人运单 → 404「无该子订单的物流信息」（实测隔离）
5. **轨迹状态机**：`Advance` 顺序推进（Created→InTransit→OutForDelivery→Signed），签收后不可再推进（400 `SHIPMENT_ALREADY_SIGNED`）；Exception 可标记（非终态），恢复后回到 InTransit；签收自动记录 `SignedAt`
6. **演示轨迹**：`POST /api/logistics/internal/tracks/advance`（X-Internal-Key）模拟物流公司回调推进轨迹，按运单号操作
7. **EF 子实体坑**：充血模型下新建子实体（ShipmentTrack，客户端 Guid 主键）通过导航集合添加时被 EF 推断为 Unchanged → 误判 UPDATE 0 行并发异常 → **必须显式 `db.Tracks.Add(track)` 标记 Added**

## 三、API 清单

### 买家端（JWT）

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/logistics/shipments/my/{subOrderId}` | 我的子订单物流（含轨迹，他人 → 404） |

### 商户端（JWT + X-Merchant-Id）

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/logistics/shipments/merchant` | 我的运单列表（status 过滤 + 分页） |
| GET | `/api/logistics/shipments/merchant/{id}` | 运单详情（含轨迹） |
| GET | `/api/logistics/shipments/merchant/companies` | 启用物流公司列表（发货选择） |

### 平台端（admin）

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/logistics/companies` | 物流公司列表（含停用，分页） |
| POST | `/api/logistics/companies` | 创建物流公司（编码唯一） |
| PUT | `/api/logistics/companies/{id}` | 更新名称 / 查询链接 |
| PUT | `/api/logistics/companies/{id}/status` | 启用 / 停用 |

### 内部接口（X-Internal-Key）

| 方法 | 路径 | 说明 |
|---|---|---|
| POST | `/api/logistics/internal/shipments` | 创建运单（order-service 发货回调） |
| POST | `/api/logistics/internal/tracks/advance` | 轨迹推进 / 标记异常（模拟物流回调） |

## 四、状态与约束

- 运单状态：1 待揽收 / 2 运输中 / 3 派送中 / 4 已签收 / 5 异常
- 运单号 6-64 字符、公司编码 2-50 字符（领域层校验）
- 已签收再推进 → 400；运单不存在 → 404；非 admin 调平台接口 → 403
- 种子数据：启动时初始化 6 家物流公司（顺丰/圆通/中通/韵达/京东/EMS，含官网查询链接模板）

## 五、联调验证（2026-08-02 实测）

```
健康检查 ✅ → 内部创建运单（公司名自动带出）✅ → 同子订单重复创建 400 ✅
轨迹推进：待揽收→运输中→派送中→签收（SignedAt 写入）✅ → 签收后再推进 400 ✅
商户发货（带物流）→ 运单自动创建（订单-物流联动）✅ → 商户运单列表/详情 ✅
买家查自己运单 ✅ → 买家查他人运单 404（隔离）✅ → 启用公司列表 ✅
平台公司列表（admin）✅ → 买家调平台接口 403 ✅
网关转发：/api/logistics/** → 8013 ✅
```

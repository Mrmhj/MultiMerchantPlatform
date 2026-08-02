# order-service — 订单微服务

> **所属阶段**：Phase 1 Week 6-7 · **优先级**：P0 · **端口**：8004
> **更新日期**：2026-08-02

## 一、职责

订单核心链路（多商户拆单）：

- 订单创建（商品可跨商户，**自动按商户拆单**）
- 订单状态机（待付款 → 已付款 → 已发货 → 已完成 / 已取消）
- 买家视角：我的订单 / 详情 / 取消 / 支付确认
- 商户视角：子订单列表 / 发货 / 完成
- 商品与价格**快照**（下单时固化，不随商品改价变化）

## 二、核心设计

### 拆单模型（多商户订单）

```
                    订单 Order（买家维度）
                  ┌─────────┴──────────┐
        SubOrder（商户A）      SubOrder（商户B）   ← 按 MerchantId 分组拆单
            │  Items                  │  Items
        OrderItem×N             OrderItem×N      ← 商品/价格快照
```

- **Order**（主单）：买家维度，总金额 = 全部子单合计，状态机独立
- **SubOrder**（子单）：商户维度，独立履约（发货/完成），按 `X-Merchant-Id` 隔离
- **OrderItem**（商品项）：商品名/SKU/规格/单价快照，小计 = 单价 × 数量

### 状态机

```
主单 Order:  Pending ──支付──▶ Paid ──全部子单完成──▶ Completed
                │
                └──取消(仅Pending)──▶ Cancelled（级联子单）

子单 SubOrder: Pending ──支付──▶ Paid ──发货──▶ Shipped ──完成──▶ Completed
                  │
                  └──取消(仅Pending)──▶ Cancelled
```

- 子单全部 Completed 后，主单自动 Completed（`TryComplete`）

### 数据库（MMP_Order 库）

| 表 | 说明 | 关键索引 |
|----|------|---------|
| `Orders` | 主订单 | `OrderNo` 唯一；`(BuyerUserId, Status)` |
| `SubOrders` | 子订单（拆单） | `(MerchantId, Status)`；`OrderId` |
| `OrderItems` | 商品项（快照） | `SubOrderId` |

## 三、REST API

### 买家订单 `/api/orders`（需登录）

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/orders` | 创建订单（跨商户自动拆单） |
| GET | `/api/orders` | 我的订单分页 |
| GET | `/api/orders/{id}` | 订单详情（含拆单） |
| POST | `/api/orders/{id}/cancel` | 取消（仅待付款） |
| POST | `/api/orders/{id}/pay` | 支付确认（模拟回调；正式版由 pay-service 调用） |

**创建订单请求体**：

```json
{
  "remark": "尽快发货",
  "items": [
    {
      "merchantId": "875dc16d-...", "merchantName": "摩登甄选旗舰店",
      "productId": "...", "productName": "北海道吐司",
      "skuId": "...", "skuCode": "HT-500G", "spec": "500g",
      "unitPrice": 19.9, "quantity": 2
    }
  ]
}
```

### 商户订单 `/api/orders/merchant`（需登录 + X-Merchant-Id）

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/orders/merchant` | 商户子订单分页（状态过滤） |
| POST | `/api/orders/merchant/{id}/ship` | 发货（已付款后） |
| POST | `/api/orders/merchant/{id}/complete` | 完成（发货后） |

### 网关入口（YARP）

```
/api/order/**  → order-service (8004)（前缀剥离）
```

## 四、配置说明（appsettings.json）

```json
{
  "ConnectionStrings": {
    "OrderDb": "Server=localhost;Database=MMP_Order;User Id=sa;Password=123456;TrustServerCertificate=True"
  },
  "Jwt": { "SecretKey": "与 identity-service 一致" }
}
```

## 五、项目结构

```
src/services/order-service/
├── Program.cs                        # JWT + Swagger(Bearer) + 自动迁移
├── Domain/
│   ├── Entities/Order.cs             # Order / SubOrder / OrderItem + 拆单逻辑（充血模型）
│   └── Enums/OrderStatus.cs
├── Application/
│   ├── Commands/                     # 创建/取消/支付 + 发货/完成
│   ├── Queries/OrderQueries.cs       # 我的订单/详情/商户子单
│   ├── OrderMapper.cs
│   └── DependencyInjection.cs
├── Infrastructure/
│   ├── HttpMerchantProvider.cs
│   └── Persistence/                  # OrderDbContext + Migrations
├── DTOs/OrderDtos.cs
├── Controllers/                      # Orders / MerchantOrders / Health
└── appsettings.json
```

## 六、已验证（冒烟测试）

| 场景 | 结果 |
|------|------|
| 健康检查 | ✅ healthy |
| 跨商户订单（2 商户 3 商品） | ✅ 自动拆 2 子单，金额正确（120.3） |
| 我的订单列表 / 详情 | ✅ |
| 支付（Pending→Paid） | ✅ 主单 + 全部子单同步 |
| 商户发货（X-Merchant-Id） | ✅ Shipped |
| 子单完成 → 主单自动完成 | ✅ 全部子单完成后 Completed |
| 部分子单完成不误判 | ✅ 主单保持 Paid（修复 EF 关系修复误判） |
| 取消已付款订单 | ✅ 400 状态不允许 |
| 取消待付款订单 | ✅ 主单 Cancelled + 子单级联 |
| Swagger UI | ✅ 8 接口 + Bearer + 全注解 |

## 七、已知限制与后续扩展

- **支付对接**：`/pay` 当前为模拟确认；正式版由 pay-service（Week 7-8）回调
- **价格信任**：当前单价由客户端提交（快照）；正式版从 product-service 拉取校验
- **库存联动**：下单不预占库存；stock-service（Week 8）接入后补充扣减/回滚
- **超时关闭**：待付款订单超时自动取消（后续后台任务）
- **物流信息**：发货含物流单号/公司（logistics-service，Phase 1 后续）
- **事件发布**：订单状态变更发布事件（messaging-service），驱动通知/结算（Phase 2 接线）

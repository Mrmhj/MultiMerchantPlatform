# stock-service — 库存微服务

> **所属阶段**：Phase 1 Week 8 · **优先级**：P0 · **端口**：8006
> **更新日期**：2026-08-02

## 一、职责

SKU 库存管理中心：

- 库存模型：总库存 / 已预占 / 可用（可用 = 总库存 - 预占）
- 商户库存管理（创建 / 查询 / 补货 / 流水审计）
- **内部接口**：预占（下单）/ 确认扣减（支付成功）/ 释放回滚（取消）— 供 order-service 接入
- 库存流水（每次变动全审计，关联业务号）

## 二、核心设计

### 库存模型

```
总库存 Total = 已售 + 预占 Reserved + 可用 Available
```

| 操作 | 触发场景 | 效果 |
|------|---------|------|
| Reserve 预占 | 下单 | Reserved +N（可用不足拒绝） |
| Confirm 扣减 | 支付成功 | Reserved -N, Total -N |
| Release 释放 | 订单取消 | Reserved -N |
| Increase 补货 | 商户操作 | Total +N |

所有变动经实体方法（充血模型），并写入 `StockTransactions` 流水（类型 + 数量 + 关联订单号）。

### 内部接口鉴权

- `X-Internal-Key` 请求头校验（配置 `Internal:Key`，与 order-service 约定一致）
- 内部接口返回 `{ success, error, stock }` 结构，库存不足返回 success=false（不下单）

### 数据库（MMP_Stock 库）

| 表 | 说明 | 关键索引 |
|----|------|---------|
| `StockItems` | 库存条目（SKU） | `SkuId` 唯一；`MerchantId` |
| `StockTransactions` | 库存流水 | `(SkuId, CreatedAt)` |

## 三、REST API

### 商户库存 `/api/stocks`（需登录 + X-Merchant-Id）

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/stocks` | 创建库存（初始总库存） |
| GET | `/api/stocks` | 库存列表（分页） |
| GET | `/api/stocks/{skuId}` | 库存详情 |
| POST | `/api/stocks/{skuId}/increase` | 补货入库 |
| GET | `/api/stocks/{skuId}/transactions` | 库存流水（审计） |

### 内部接口 `/api/stocks/internal`（X-Internal-Key）

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/stocks/internal/reserve` | 预占（下单回调） |
| POST | `/api/stocks/internal/confirm` | 确认扣减（支付回调） |
| POST | `/api/stocks/internal/release` | 释放预占（取消回调） |

**内部请求体**：

```json
{ "skuId": "99999999-...", "quantity": 30, "referenceId": "ORD20260802..." }
```

### 网关入口（YARP）

```
/api/stock/**  → stock-service (8006)（前缀剥离）
```

## 四、配置说明（appsettings.json）

```json
{
  "ConnectionStrings": { "StockDb": "Server=localhost;Database=MMP_Stock;User Id=sa;Password=123456;TrustServerCertificate=True" },
  "Jwt": { "SecretKey": "与 identity-service 一致" },
  "Internal": { "Key": "MMP-Internal-Key-2026" }
}
```

## 五、项目结构

```
src/services/stock-service/
├── Program.cs                        # JWT + Swagger(Bearer) + 自动迁移
├── Domain/
│   ├── Entities/StockItem.cs         # StockItem + StockTransaction（充血模型）
│   └── Enums/StockTransactionType.cs
├── Application/
│   ├── Commands/                     # 商户（创建/补货）+ 内部（预占/扣减/释放）
│   ├── Queries/StockQueries.cs       # 列表/详情/流水
│   ├── StockMapper.cs
│   └── DependencyInjection.cs
├── Infrastructure/
│   ├── HttpMerchantProvider.cs
│   └── Persistence/                  # StockDbContext + Migrations
├── DTOs/StockDtos.cs
├── Controllers/                      # Stocks / InternalStocks / Health
└── appsettings.json
```

## 六、已验证（冒烟测试）

| 场景 | 结果 |
|------|------|
| 健康检查 | ✅ healthy |
| 创建库存（100） | ✅ total 100 / available 100 |
| 内部预占 30 | ✅ reserved 30 / available 70 |
| 预占超量 100 | ✅ 库存不足保护（success=false） |
| 确认扣减 20 | ✅ total 80 / reserved 10 / available 70 |
| 释放预占 10 | ✅ reserved 0 / available 80 |
| 补货 50 | ✅ total 130 |
| 内部密钥错误 | ✅ 401 |
| 库存流水 | ✅ 5 条全审计 |
| Swagger UI | ✅ 8 接口 + Bearer + 全注解 |

## 七、已知限制与后续扩展

- **订单联动待接线**：order-service 下单调 reserve、支付确认调 confirm、取消调 release（本阶段内部接口就绪，接入在 Phase 1 收尾统一进行）
- **并发安全**：当前依赖单机数据库行锁；高并发场景需引入分布式锁/乐观并发（Performance 阶段评估）
- **超卖防护**：预占 + 可用校验已防超卖，跨服务最终一致性靠补偿
- **库存告警**：低库存告警通知（notification-service，Phase 2）

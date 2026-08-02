# pay-service — 支付微服务

> **所属阶段**：Phase 1 Week 7-8 · **优先级**：P0 · **端口**：8005
> **更新日期**：2026-08-02

## 一、职责

支付网关（当前模拟渠道，正式对接第三方支付）：

- 支付单管理（创建 / 查询 / 状态机）
- 支付确认（模拟渠道支付成功）
- **跨服务联动**：支付成功回调 order-service 确认订单已付款（服务间同步调用）
- 退款（支付成功后）

## 二、核心设计

### 支付状态机

```
Pending ──支付成功──▶ Success ──退款──▶ Refunded
   │
   └──支付失败──▶ Failed
```

### 跨服务回调（首次服务间同步调用）

```
买家: 创建订单(order-service) → 创建支付单(pay-service) → 模拟支付(pay-service)
                                                            │
                                    MarkSuccess + 回调 order-service
                                                            ▼
                        IServiceClient.PostAsync(/api/orders/{id}/pay-internal)
                        （X-Internal-Key 默认头校验服务身份）
                                                            ▼
                                            订单状态 → Paid
```

- 使用 `BuildingBlocks.Communication` 的 `IServiceClient`（HTTP 策略，Strategy 模式可切 gRPC）
- order-service 内部端点 `pay-internal` 用 `X-Internal-Key` 校验，不走买家鉴权
- 回调失败不阻塞支付状态（日志记录，供后续补偿）

### 数据库（MMP_Pay 库）

| 表 | 说明 | 关键索引 |
|----|------|---------|
| `Payments` | 支付单 | `PayNo` 唯一；`(BuyerUserId, Status)`；`OrderId` |

## 三、REST API

### 支付 `/api/payments`（需登录）

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/payments` | 创建支付单（同订单仅一笔待支付） |
| GET | `/api/payments` | 我的支付单分页 |
| GET | `/api/payments/{id}` | 支付单详情 |
| POST | `/api/payments/{id}/simulate-pay` | 模拟支付（成功后回调订单） |
| POST | `/api/payments/{id}/refund` | 退款（仅支付成功后） |

### 内部接口（order-service 新增）

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/orders/{id}/pay-internal` | 支付确认（X-Internal-Key 校验，供 pay-service 回调） |

### 网关入口（YARP）

```
/api/pay/**  → pay-service (8005)（前缀剥离）
```

## 四、配置说明（appsettings.json）

```json
{
  "ConnectionStrings": { "PayDb": "Server=localhost;Database=MMP_Pay;User Id=sa;Password=123456;TrustServerCertificate=True" },
  "Jwt": { "SecretKey": "与 identity-service 一致" },
  "Services": { "OrderService": { "BaseUrl": "http://localhost:8004", "Protocol": "Http" } },
  "Internal": { "Key": "MMP-Internal-Key-2026" }
}
```

> **注意**：`Internal.Key` 必须与 order-service 的 `Internal:Key` 一致。

## 五、项目结构

```
src/services/pay-service/
├── Program.cs                        # JWT + Swagger(Bearer) + 自动迁移
├── Domain/
│   ├── Entities/Payment.cs           # 支付单状态机（充血模型）
│   └── Enums/PaymentStatus.cs
├── Application/
│   ├── Commands/PaymentCommands.cs   # 创建/模拟支付/退款
│   ├── Queries/PaymentQueries.cs     # 列表/详情
│   ├── PaymentMapper.cs
│   └── DependencyInjection.cs        # 注册命名 HttpClient（带 X-Internal-Key）
├── Infrastructure/
│   ├── OrderServiceClient.cs         # 跨服务回调封装（IServiceClient）
│   └── Persistence/                  # PayDbContext + Migrations
├── DTOs/PayDtos.cs
├── Controllers/                      # Payments / Health
└── appsettings.json
```

## 六、已验证（冒烟测试）

| 场景 | 结果 |
|------|------|
| 健康检查 | ✅ healthy |
| 创建支付单（关联订单） | ✅ Pending，同订单重复创建拦截 |
| 模拟支付 | ✅ 状态 Success |
| **跨服务回调订单** | ✅ 订单状态自动变 Paid（IServiceClient 调用成功） |
| 退款 | ✅ Refunded |
| 重复支付/退款后支付 | ✅ 400 状态保护 |
| Swagger UI | ✅ 5 接口 + Bearer + 全注解 |

## 七、已知限制与后续扩展

- **真实渠道**：当前 `simulate-pay` 为模拟渠道；正式接入微信/支付宝需实现渠道适配（Strategy 模式扩展点）
- **回调验签**：真实第三方异步回调需验签 + 幂等（当前模拟直调）
- **退款联动**：退款后应同步 order-service 订单退款状态（后续事件/回调）
- **超时关单**：待支付超时自动关闭（后续后台任务）
- **对账**：每日对账（pay vs order）待 Phase 2

# messaging-service — 自封装消息队列微服务

> **所属阶段**：Phase 0 Week 2 · **优先级**：P0 · **端口**：8010
> **更新日期**：2026-08-02

## 一、职责

为平台所有微服务提供**可靠的消息投递**（集成事件），替代 RabbitMQ 等外部中间件：

- 消息持久化（Outbox 模式：先落库，再异步投递，不丢消息）
- 发布订阅（Publish/Subscribe）：一个事件可被多个订阅者消费
- 至少一次投递 + 幂等去重（防止重复处理）
- 指数退避重试 + 死信队列（超过最大重试次数自动转死信）
- 管理 API：发布 / 状态查询 / 手动重试 / 订阅管理

## 二、核心设计

```
┌────────────────────────────────────────────┐
│              messaging-service (8010)       │
│                                            │
│  ┌──────────────┐   ┌───────────────────┐  │
│  │ REST API      │   │ MessageDispatcher │  │
│  │ 发布/查询/重试 │   │ (BackgroundService)│  │
│  └──────┬───────┘   │  轮询 Pending 消息   │  │
│         │           │  → HTTP 回调订阅者    │  │
│         │           └─────────┬─────────┘  │
│  ┌──────▼─────────────────────▼─────────┐  │
│  │      SQL Server · MMP_Infra 库        │  │
│  │  MessageOutbox │ Subscription │      │  │
│  │  Idempotency                        │  │
│  └─────────────────────────────────────┘  │
└──────────────┬─────────────────────────────┘
               │ POST 回调（幂等头 X-Message-Id）
               ▼
        订阅者微服务（BuildingBlocks.Messaging）
```

### 数据库表（MMP_Infra 库）

| 表 | 说明 | 关键字段 / 索引 |
|----|------|----------------|
| `MessageOutbox` | 消息发件箱 | MessageId(唯一) / Status / NextRetryTime；索引 `(Status, NextRetryTime)` 供轮询 |
| `MessageSubscription` | 订阅者注册 | EventName + CallbackUrl 唯一；EventName 支持 `*` 通配订阅全部事件 |
| `MessageIdempotency` | 幂等记录 | MessageId + ConsumerUrl 唯一；记录已成功消费的 (消息, 订阅者) |

### 消息状态机

```
Pending ──投递成功──▶ Published
   │
   └──投递失败──▶ Failed ──重试(指数退避)──▶ Pending
                       └──超限(≥MaxRetryCount)──▶ DeadLetter ──手动重试──▶ Pending
```

### 重试策略（可配置）

- 基准间隔 5s，第 N 次重试间隔 = `5 × 2^(N-1)`，上限 300s
- 默认最大重试 5 次，超出转死信；订阅者可单独覆盖 `maxRetryCount`

## 三、REST API

### 消息管理 `/api/messages`

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/messages` | 发布消息（Outbox 落库，异步投递） |
| POST | `/api/messages/batch` | 批量发布 |
| GET | `/api/messages/{id}` | 按 Id 查询消息状态 |
| GET | `/api/messages?status=&eventName=&page=&pageSize=` | 分页查询（支持状态/事件过滤） |
| POST | `/api/messages/{id}/retry` | 手动重试（死信/失败消息重置为待发送） |
| POST | `/api/messages/{id}/deadletter?reason=` | 手动转死信 |

**发布请求体**：

```json
{
  "eventName": "order.created",
  "payload": "{\"orderId\":\"ord-001\",\"amount\":199.00}",
  "routingKey": "order.created",
  "messageId": "3c124a10-...（可选，幂等控制）",
  "maxRetryCount": 5
}
```

### 订阅管理 `/api/subscriptions`

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/subscriptions` | 注册订阅（EventName+CallbackUrl 幂等） |
| GET | `/api/subscriptions?eventName=&active=` | 查询订阅 |
| DELETE | `/api/subscriptions/{id}` | 取消订阅（软停用） |
| POST | `/api/subscriptions/{id}/activate` | 启用订阅 |

### 健康检查

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/health` | 服务存活 + 数据库连通性 |

### 网关入口（YARP）

```
/api/messages/**      → messaging-service
/api/subscriptions/** → messaging-service
/api/health/**        → messaging-service
```

## 四、订阅者接入（消费者端）

### 1. 注册订阅（启动时或管理端调用）

```json
POST /api/subscriptions
{
  "eventName": "order.created",
  "callbackUrl": "http://order-service:8004/api/messages/consume/order-created",
  "serviceName": "order-service"
}
```

### 2. 实现消费者（业务服务内）

```csharp
// 1) 定义集成事件（BuildingBlocks.Core.Events）
public sealed record OrderCreatedEvent : IntegrationEvent
{
    public required string OrderId { get; init; }
    public decimal Amount { get; init; }
}

// 2) 继承 MessageConsumer<T>，实现业务处理
public sealed class OrderCreatedConsumer : MessageConsumer<OrderCreatedEvent>
{
    protected override Task HandleAsync(OrderCreatedEvent message, CancellationToken ct)
    {
        // 业务处理（可读取 message.MessageId 作为幂等键配合 DB 唯一约束）
        return Task.CompletedTask;
    }
}

// 3) Controller 接收端点（供 messaging-service 回调）
[HttpPost("consume/order-created")]
public async Task<IActionResult> Consume([FromBody] MessageEnvelope envelope)
{
    var result = await _consumer.ConsumeAsync(envelope, HttpContext.RequestAborted);
    return result.IsSuccess ? Ok() : StatusCode(500, result.Error);
}
```

### 3. 发布消息（业务服务内）

```csharp
// Program.cs 注册（生产默认 HTTP 方式）
builder.Services.AddHttpMessageBus(o => o.BaseUrl = "http://messaging-service:8010");
// 或开发环境内存方式：builder.Services.AddInMemoryMessageBus();

// 业务代码
public class OrderService(IMessagePublisher publisher)
{
    public async Task CreateOrderAsync(Order order, CancellationToken ct)
    {
        await publisher.PublishAsync(new OrderCreatedEvent
        {
            OrderId = order.Id.ToString(),
            Amount = order.TotalAmount,
        }, ct: ct);
    }
}
```

> **幂等提示**：messaging-service 在收到订阅者 2xx 后写幂等记录；但网络超时场景可能"已处理但未记录"，订阅者端建议用 `X-Message-Id`（envelope.Id）作为业务幂等键（数据库唯一约束）。

## 五、配置说明（appsettings.json）

```json
{
  "ConnectionStrings": {
    "MessagingDb": "Server=localhost;Database=MMP_Infra;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "Messaging": {
    "PollIntervalSeconds": 5,        // 分发器轮询间隔
    "BatchSize": 100,                 // 每批处理消息数
    "DefaultMaxRetryCount": 5,        // 默认最大重试次数
    "RetryBaseIntervalSeconds": 5,    // 指数退避基准间隔
    "MaxRetryDelaySeconds": 300,      // 重试最大间隔上限
    "HttpClientTimeoutSeconds": 30    // 投递请求超时
  }
}
```

## 六、项目结构

```
src/services/messaging-service/
├── Program.cs                       # 入口 + 启动时自动迁移（开发环境）
├── appsettings.json
├── Domain/
│   ├── Entities/                    # MessageOutbox / MessageSubscription / MessageIdempotency
│   └── Enums/                       # MessageStatus
├── Application/
│   ├── Options/MessagingOptions.cs  # 配置绑定
│   ├── MessagePublisher.cs          # 发布器（Outbox 落库）
│   ├── MessageDispatcher.cs         # 后台分发器（轮询/投递/重试/死信）
│   ├── SubscriptionManager.cs       # 订阅管理
│   └── DependencyInjection.cs       # DI 注册
├── Infrastructure/
│   └── Persistence/MessagingDbContext.cs
├── DTOs/MessagingDtos.cs
├── Controllers/                     # Messages / Subscriptions / Health
└── Migrations/                      # EF Core 迁移
```

## 七、已完成的验证（冒烟测试）

| 场景 | 结果 |
|------|------|
| 健康检查（数据库连通） | ✅ 200 healthy |
| 发布消息（Outbox 落库） | ✅ Pending 状态，NextRetryTime 已设置 |
| 注册订阅 | ✅ 幂等注册 |
| 后台分发 → 回调订阅者 | ✅ 收到 POST 200 |
| 消息终态 | ✅ Published（publishedAt 记录，幂等记录写入） |
| 一事件多订阅者 | ✅ 两个订阅者均收到投递 |

## 八、已知限制与后续扩展

- **多实例并发**：当前分发器为单实例轮询，多实例部署需加租约锁（`LeaseUntil` 字段）防止重复取消息
- **消费者端 ACK 语义**：采用"至少一次 + 幂等表"，未实现 NACK/死信回传（后续可按需加）
- **传输层扩展**：`BuildingBlocks.Messaging` 已提供 In-Memory / HTTP 两种策略，可再加 RabbitMQ 策略（Strategy 模式）
- **消息体校验**：Payload 目前透传 JSON 字符串，未强校验 Schema（后续可由发布方约定）
- **鉴权**：当前未加 JWT 认证（内网服务间调用），网关暴露公网时需补充

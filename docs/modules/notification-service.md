# notification-service 模块文档

> **文档路径**：`docs/modules/notification-service.md`
> **版本**：v6.5 · 2026-08-02 · **端口 8019 · 数据库 MMP_Notification（独立库）**
> **定位**：P2 平台支撑服务 — 通知中心（站内信 / 短信 / App Push）+ 通知模板 + SignalR 实时推送

---

## 一、职责概述

| 能力 | 说明 |
|------|------|
| **站内信** | 通知中心收件箱：按用户隔离（JWT sub），支持业务类型/已读状态过滤、未读数统计、单条已读/全部已读/软删除 |
| **模板渲染** | 通知模板（唯一编码 + 标题/内容模板 + 占位符 `{变量}` 替换），内部接口按 Code 一键渲染发送；模板管理接口（admin）CRUD/启停 |
| **短信（SMS）** | 独立渠道记录（DryRun 模式默认开启：仅落库标记成功，不真实下发），状态机 Pending→Sent/Failed/DeadLetter，预留真实网关扩展点 |
| **App Push** | 独立渠道记录（DryRun 模式默认开启），状态机与短信一致，预留极光/个推/APNs/FCM 扩展点 |
| **实时推送** | SignalR Hub（`/hub/notification`）：新通知实时推送到用户全部在线连接（按 JWT sub 定向），标记已读后推送未读数变化 |
| **内部接入** | 内部接口（X-Internal-Key）：发站内信（直接内容或模板渲染）/ 发短信 / 发 Push —— 供 order / logistics / performance / logging / risk 等系统服务接入告警与业务通知 |

---

## 二、技术架构

```
┌────────────────────────────────────────────────────────┐
│                notification-service (8019)              │
│                                                        │
│  ┌─────────────────┐      ┌─────────────────────────┐  │
│  │ 用户端接口        │      │ 内部接口 (X-Internal-Key) │  │
│  │ /api/           │      │ /api/notifications/      │  │
│  │  notifications/ │      │   internal/send(站内信)   │  │
│  │   列表/未读数/已读 │      │   internal/sms          │  │
│  │   全部已读/删除   │      │   internal/push         │  │
│  └────────┬────────┘      └───────────┬─────────────┘  │
│           │                          │               │
│           ▼                          ▼               │
│  ┌───────────────────────────────────────────────┐    │
│  │  NotificationSender（模板渲染→落库→实时推送）      │    │
│  │  SmsSender / PushSender（DryRun 模拟通道）        │    │
│  └──────────────────┬────────────────────────────┘    │
│                     │                                 │
│  ┌──────────────────▼────────────────────────────┐    │
│  │  NotificationDispatcher（IHubContext 定向推送） │    │
│  │  → NotificationHub (/hub/notification)        │    │
│  └───────────────────────────────────────────────┘    │
│                                                        │
│  存储：MMP_Notification（独立库，4 张表）                 │
│    Notifications / NotificationTemplates /            │
│    SmsMessages / PushMessages                         │
└────────────────────────────────────────────────────────┘
```

### 分层（Mediator + CQRS 强制）

```
Controller → IMediator → ICommandHandler / IQueryHandler → Domain 实体（充血）→ NotificationDbContext
```

- 写操作：SendInApp（模板渲染或直接内容 + 实时推送）、SendSms、SendPush、MarkRead、MarkAllRead、
  Delete（软删除）、模板 Create/Update/Delete/SetEnabled
- 读操作：我的通知分页（类型/已读过滤）、未读数统计、模板列表/详情
- 实时通道：`NotificationHub`（Authorize，WebSocket 令牌走 query `access_token`，`CustomUserIdProvider` 按 sub 定向）
- 渠道发送器：`SmsSender` / `PushSender`（Scoped，DryRun 直接 `MarkSent`，真实模式扩展点）

---

## 三、数据库设计（MMP_Notification 库）

### Notifications — 站内信通知（用户收件箱）

| 字段 | 类型 | 说明 |
|------|------|------|
| UserId | uniqueidentifier | 接收用户 ID（收件箱隔离维度） |
| MerchantId | uniqueidentifier? | 业务归属商户 ID（平台级通知为空） |
| Type | int | 业务类型：Order=1 / Payment=2 / Logistics=3 / Promotion=4 / System=5 / Risk=6 / Monitor=7 |
| Title | nvarchar(200) | 标题（1-200 字符） |
| Content | nvarchar(2000) | 内容（1-2000 字符） |
| BizType | nvarchar(50)? | 业务类型编码（ORDER_PAID 等，可选） |
| BizId | nvarchar(100)? | 业务单据 ID（订单号等，可选） |
| Channel | int | 来源渠道（默认 InApp=1） |
| IsRead / ReadAt | bit / datetime2? | 已读标记与时间 |
| IsDeleted | bit | 软删除（移出收件箱，保留审计） |

索引：`(UserId, IsDeleted, CreatedAt)` 收件箱分页、`(UserId, IsRead, IsDeleted)` 未读数、`(MerchantId, IsDeleted, CreatedAt)` 平台/商户筛选、`BizType`。

### NotificationTemplates — 通知模板（平台级配置）

| 字段 | 类型 | 说明 |
|------|------|------|
| Code | nvarchar(50) | 模板编码（唯一，仅字母/数字/下划线，如 ORDER_PAID） |
| TitleTemplate / BodyTemplate | nvarchar(500/2000) | 标题/内容模板（可含 `{变量}` 占位符，渲染时大小写不敏感替换，未知变量替换为空） |
| Channels | int | 适用渠道（位标志：InApp=1 / Sms=2 / Push=4，可组合） |
| Description | nvarchar(500)? | 模板说明 |
| IsActive | bit | 是否启用（停用后内部发送端拒绝使用 → TEMPLATE_NOT_FOUND） |

### SmsMessages — 短信发送记录

| 字段 | 类型 | 说明 |
|------|------|------|
| Phone | nvarchar(20) | 接收手机号 |
| Content | nvarchar(500) | 短信内容 |
| Status | int | Pending=0 / Sent=1 / Failed=2 / DeadLetter=3 |
| RetryCount / MaxRetryCount | int | 重试计数与上限（默认 3，可配置） |
| LastError | nvarchar(1000)? | 最近错误信息 |
| SentAt | datetime2? | 发送时间 |

### PushMessages — App Push 推送记录

| 字段 | 类型 | 说明 |
|------|------|------|
| DeviceToken | nvarchar(256) | 设备令牌 |
| Title / Content | nvarchar(200/1000) | 推送标题 / 内容 |
| Status | int | Pending=0 / Sent=1 / Failed=2 / DeadLetter=3 |
| RetryCount / MaxRetryCount | int | 重试计数与上限 |
| LastError / SentAt | — | 错误信息 / 推送时间 |

### Announcements — 平台公告（v6.6 新增，广播模型）

| 字段 | 类型 | 说明 |
|------|------|------|
| Title | nvarchar(200) | 标题（1-200 字符） |
| Content | nvarchar(5000) | 正文（1-5000 字符） |
| Category | int | 分类：System=1 / Operation=2 / Maintenance=3 |
| PublisherUserId | uniqueidentifier | 发布者用户 ID（平台 admin） |
| PublisherName | nvarchar(100) | 发布者名称（JWT UniqueName = 登录邮箱） |
| Status | int | Draft=0（预留）/ Published=1 / Offline=2 |
| PublishedAt / OfflineAt | datetime2? | 发布时间 / 下线时间 |

索引：`(Status, PublishedAt)` 列表倒序、`(Status, Category, PublishedAt)` 分类筛选。
与站内信（一对一复制到收件箱）互补：**公告一对多广播，不复制到用户收件箱**，已读状态惰性记录。

### AnnouncementReads — 公告已读记录（用户维度）

| 字段 | 类型 | 说明 |
|------|------|------|
| AnnouncementId + UserId | 复合主键 | 公告 ID + 用户 ID（唯一，重复标记幂等） |
| ReadAt | datetime2 | 已读时间 |

索引：`UserId`（未读数按用户统计）。未读 = 已发布公告中无已读记录的公告数（下线公告不计入）。

---

## 四、API 概览

### 用户端（JWT 登录，数据按 sub 隔离）

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/notifications` | 我的通知分页（type / isRead 过滤） |
| GET | `/api/notifications/unread-count` | 未读数（角标轮询/同步） |
| POST | `/api/notifications/{id}/read` | 标记单条已读（非本人 404） |
| POST | `/api/notifications/read-all` | 全部标记已读 |
| DELETE | `/api/notifications/{id}` | 删除单条（软删除） |

### 公告接口（v6.6 新增）

| 方法 | 路径 | 权限 | 说明 |
|------|------|------|------|
| POST | `/api/notifications/announcements` | admin | 发布公告（创建即发布，SignalR 全量广播） |
| POST | `/api/notifications/announcements/{id}/offline` | admin | 下线公告（下线后用户不可见，未读不计入） |
| GET | `/api/notifications/announcements` | 登录 | 公告分页（category 过滤，含当前用户已读状态） |
| GET | `/api/notifications/announcements/unread-count` | 登录 | 公告未读数（顶栏角标） |
| GET | `/api/notifications/announcements/{id}` | 登录 | 公告详情（未发布/已下线 → 400 ANNOUNCEMENT_NOT_AVAILABLE） |
| POST | `/api/notifications/announcements/{id}/read` | 登录 | 标记已读（幂等 upsert） |

### 内部接口（X-Internal-Key）

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/notifications/internal/send` | 发站内信：`templateCode + templateData` 渲染，或直接 `title + content`；`realtime=true` 时经 SignalR 实时送达 |
| POST | `/api/notifications/internal/sms` | 发短信（DryRun 默认 true） |
| POST | `/api/notifications/internal/push` | 发 App Push（DryRun 默认 true） |

### 模板管理（admin 角色）

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/notifications/templates` | 模板列表（activeOnly 过滤） |
| GET | `/api/notifications/templates/{id}` | 模板详情 |
| POST | `/api/notifications/templates` | 创建模板（Code 唯一） |
| PUT | `/api/notifications/templates/{id}` | 更新模板 |
| POST | `/api/notifications/templates/{id}/enabled?enabled=` | 启停模板 |
| DELETE | `/api/notifications/templates/{id}` | 删除模板（物理） |

### 实时通道

- **Hub**：`/hub/notification`（WebSocket，`?access_token=<jwt>` 携带令牌）
- **服务端 → 客户端**：`ReceiveNotification(NotificationResponse)`、`UnreadCountChanged(int)`、`ReceiveAnnouncement(AnnouncementResponse)`
- **网关**：`/api/notifications/{**catch-all}` + `/hub/notification/{**catch-all}` → notification-cluster (8019)

---

## 五、默认模板种子（首次启动幂等写入）

| Code | 场景 | 渠道 | 占位符 |
|------|------|------|--------|
| ORDER_PAID | 订单支付成功（买家） | InApp+Sms+Push | OrderNo / Amount |
| ORDER_SHIPPED | 订单发货（买家） | InApp+Sms+Push | OrderNo / Company / TrackingNo |
| ORDER_CREATED | 新订单提醒（商户） | InApp+Push | OrderNo / Amount |
| PAYMENT_REFUNDED | 退款成功（买家） | InApp+Sms+Push | OrderNo / Amount |
| SYSTEM_ANNOUNCEMENT | 平台公告 | InApp | Content |
| RISK_ALERT | 风控规则命中告警（管理员） | InApp+Push | RuleName / Hits / Scene |
| MONITOR_ALERT | 监控指标告警（管理员） | InApp+Push | ServiceName / Metric / Value / Threshold |
| SMS_VERIFY_CODE | 短信验证码 | Sms | Code / Minutes |

---

## 六、对接示例（其他服务发送通知）

```bash
# 站内信（模板渲染）：订单支付成功
curl -X POST http://localhost:8019/api/notifications/internal/send \
  -H "X-Internal-Key: MMP-Internal-Key-2026" -H "Content-Type: application/json" \
  -d '{"userId":"<买家ID>","type":1,"templateCode":"ORDER_PAID",
       "templateData":{"OrderNo":"ORD20260801","Amount":"99.50"},"realtime":true}'

# 短信（DryRun 模拟）
curl -X POST http://localhost:8019/api/notifications/internal/sms \
  -H "X-Internal-Key: MMP-Internal-Key-2026" -H "Content-Type: application/json" \
  -d '{"phone":"13800138000","content":"【多商户平台】您的验证码是 123456"}'
```

**告警接入规划**（后续阶段）：performance-service `AlertEvaluator` / logging-service Error 日志自动触发
`MONITOR_ALERT` 模板站内信 + Push（当前以日志 + 落库为准，notification-service 已就绪可直接接入）。

---

## 七、已知边界与扩展点

- **短信/Push 为 DryRun 模拟**：本地无真实网关，仅落库标记 Sent；接入生产需在 `SmsSender.SendAsync` /
  `PushSender.SendAsync` 中实现第三方调用（阿里云/腾讯云短信、极光/个推/APNs/FCM），失败抛异常自动转 Failed/DeadLetter 状态机
  （**生产网关接入方案暂缓**，本阶段仅交付内部公告 + 内部邮件；外部短信/Push 后续按需接入）
- **公告为广播模型**：不复制到用户收件箱，已读状态惰性写入 AnnouncementReads；发布时经 SignalR `Clients.All`
  广播给在线用户，离线用户下次登录从列表接口拉取
- **站内信为平台级用户维度**：不按商户隔离（通知属于用户个人收件箱），`MerchantId` 仅为业务归属标记，
  平台级通知为空；商户维度查询走 `(MerchantId, IsDeleted, CreatedAt)` 索引
- **保留期限**：`Notification.RetentionDays`（默认 180 天），过期站内信可由归档任务清理（当前未启用）
- **网关 /api/health 歧义**：多服务共用同一 health 路由（既有行为），健康检查请直连服务端口

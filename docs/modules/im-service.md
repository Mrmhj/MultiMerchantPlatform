# im-service 即时通讯服务

> 模块文档 · 摩登时代 · 2026-08-02 · Phase 2 Week 12

## 一、概述

| 项 | 值 |
|---|---|
| 端口 | **8016** |
| 数据库 | `MMP_IM`（ChatSessions / ChatSessionMembers / ChatMessages） |
| 实时通道 | SignalR WebSocket Hub `/hub/chat`（JWT 鉴权，`access_token` query 携带令牌） |
| 网关路由 | `/api/im/**`（直通，无前缀剥离）+ `/hub/chat/**`（WebSocket 转发） |
| 买家端 | 会话列表 / 创建私聊 / 历史消息 / 已读 / 发送（JWT） |
| 商户端 | 会话列表 / 客服群 / 历史消息 / 已读 / 回复（JWT + `X-Merchant-Id` 头） |
| 内部接口 | 系统通知推送（订单/物流状态），X-Internal-Key 校验 |
| 客户端 SDK | Web/Electron：`@microsoft/signalr`；uni-app：SignalR（H5/小程序）+ 原生 WebSocket（App） |

**定位**：即时通讯域——C 端买家与商户客服的私聊、商户客服群聊，以及订单/物流等系统通知的**实时推送 + 离线消息兜底**。消息落库可查，多端实时同步。

## 二、核心设计

### 实体模型

```
ChatSession（多租户：MerchantId，聚合根）
 ├─ Type：Private（私聊）/ Group（群聊，客服群）
 ├─ Status：Active（进行中）→ Closed（已关闭，仅可查历史）
 ├─ LastMessageAt / LastMessagePreview（列表摘要，自动截断 200 字符）
 └─ Members：ChatSessionMembers（一用户一会话一记录，SessionId+UserId 唯一）

ChatSessionMember
 ├─ SessionId / UserId / UserName（快照）/ Role（Buyer / MerchantStaff / Admin / System）
 └─ UserId 索引（按用户反查参与的会话）

ChatMessage（多租户：MerchantId）
 ├─ SessionId / SenderId / SenderName（快照）/ SenderRole
 ├─ MessageType：Text / Image / File / OrderCard / System
 ├─ Content（≤4000 字符：文本 / 图片文件 URL / 订单卡片 JSON）
 └─ IsRead / ReadAt（已读回执，幂等置位）
```

### 实时通道（SignalR）

```
Web 端（@microsoft/signalr）──WS──▶ /hub/chat?access_token=xxx
                                          │
  ┌───────────────────────────────────────▼────────────────────┐
  │ ChatHub（[Authorize]，IUserIdProvider 从 JWT sub 解析用户）  │
  │  OnConnectedAsync  → 注册连接 + 加入全部会话组 + 补推离线消息 │
  │  SendMessage()     → 落库（成员角色权威化） + 群组广播       │
  │  MarkAsRead()      → 批量置已读 + 广播已读回执              │
  │  SendTypingIndicator() → 输入中指示转发（不落库）            │
  │  OnDisconnectedAsync → 清理连接                             │
  └───────────────────────────────────────┬────────────────────┘
                                          │ 强类型 IChatClient
        ReceiveMessage / MessageRead / TypingIndicator（推送方法）
```

- **会话即分组**：私聊/群聊统一按 `Group(sessionId)` 广播，上线时加入自己全部会话组
- **在线判断**：`UserConnectionManager`（内存 用户ID↔连接ID 映射），供内部推送判断是否实时送达
- **离线兜底**：`OnConnectedAsync` 补推该用户参与会话内的**未读消息**（≤50 条，越权防护：仅限本人会话成员的消息）

### 关键规则

1. **成员角色权威化**：发送消息时发送者角色以**会话成员表**为准（Hub 的 JWT 推断仅兜底）——未提权的客服账号在 Hub 发送时仍按成员角色 MerchantStaff 落库
2. **私聊幂等**：`GET 或创建`——按（MerchantId + 双方用户）双向查找已有活跃会话，无则创建并加入双方成员（买家=Buyer，对方=MerchantStaff）
3. **多租户三重防护**（商户维度）：`MultiTenantEntity` + HasQueryFilter（X-Merchant-Id 非空时生效）+ Handler 显式过滤；缺 `X-Merchant-Id` → 400 `MERCHANT_REQUIRED`；买家接口按 `CurrentUserId` 成员隔离
4. **成员校验**：发送 / 已读 / 查历史前校验请求者是会话成员（非成员 → 400 `NOT_SESSION_MEMBER`；REST 层 404）
5. **系统通知定位**：内部推送优先落指定会话 → 否则用户在该商户的最近活跃会话 → 再无则新建「系统通知」会话（单成员）
6. **游标分页**：历史消息按 `(CreatedAt, Id)` 字典序游标，最新在前，`hasMore` 标记更早消息存在

## 三、API 清单

### 买家端（JWT）

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/im/sessions` | 我的会话列表（含未读数，最新在前） |
| POST | `/api/im/sessions/private` | 获取或创建私聊会话（幂等：已有活跃会话直接返回） |
| GET | `/api/im/sessions/{id}/messages` | 历史消息（beforeId 游标 + limit 分页，最新在前） |
| POST | `/api/im/sessions/{id}/read` | 标记会话全部已读（广播已读回执） |
| POST | `/api/im/sessions/{id}/send` | 发送消息（REST 兜底通道，等价 Hub SendMessage） |

### 商户端（JWT + X-Merchant-Id）

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/api/im/merchant/sessions` | 本商户会话列表（未读数=买家/系统发来的消息） |
| POST | `/api/im/merchant/groups` | 创建客服群聊（发起人自动加入，成员去重，≥2 人） |
| GET | `/api/im/merchant/sessions/{id}/messages` | 历史消息（校验商户归属） |
| POST | `/api/im/merchant/sessions/{id}/read` | 标记会话全部已读 |
| POST | `/api/im/merchant/sessions/{id}/reply` | 回复消息（角色=MerchantStaff） |

### SignalR Hub（/hub/chat）

| 方法（客户端 invoke） | 参数 | 说明 |
|---|---|---|
| SendMessage | sessionId, content, messageType? | 发送消息，返回落库消息 DTO |
| MarkAsRead | sessionId | 标记已读并广播回执 |
| SendTypingIndicator | sessionId | 输入中指示（转发其他成员） |

| 推送方法（服务端 → 客户端） | 说明 |
|---|---|
| ReceiveMessage(message) | 实时新消息 / 上线补推离线消息 |
| MessageRead(sessionId, readerUserId, markedCount) | 已读回执 |
| TypingIndicator(sessionId, senderId, senderName) | 输入中指示 |

### 内部接口（X-Internal-Key）

| 方法 | 路径 | 说明 |
|---|---|---|
| POST | `/api/im/internal/push` | 系统通知推送（订单/物流状态等），定位会话 → 落库 → 实时推送，返回 delivered（是否在线） |

## 四、状态与约束

- 会话类型：1 私聊 / 2 群聊；会话状态：1 进行中 / 2 已关闭
- 消息类型：1 文本 / 2 图片 / 3 文件 / 4 订单卡片 / 5 系统
- 成员角色：1 买家 / 2 商户客服 / 3 平台管理员 / 4 系统
- 消息内容 ≤ 4000 字符；会话摘要 ≤ 200 字符；群聊成员 ≥ 2 人

## 五、多端接入示例

```js
// Web / uni-app(H5)：JWT 通过 query access_token 携带（WebSocket 无法带 Header）
import * as signalR from '@microsoft/signalr';
const conn = new signalR.HubConnectionBuilder()
  .withUrl(`https://gw/api-im/hub/chat?access_token=${token}`)
  .build();
conn.on('ReceiveMessage', msg => { /* 新消息（含离线补推） */ });
conn.on('MessageRead', (sessionId, readerUserId) => { /* 已读回执 */ });
conn.on('TypingIndicator', (sessionId, senderId, senderName) => { /* 对方输入中 */ });
await conn.start();
await conn.invoke('SendMessage', sessionId, '你好', 1);
```

## 六、测试与验证

- REST 冒烟 `tests/smoke-im.sh`（26 项）：健康 / 鉴权 401 / 创建私聊+幂等 / 发送+空内容 400 / 未读数 / 历史分页 / 已读回执 / 商户回复 / 商户会话 / 缺头 400 / 客服群 / 内部推送 / 通知并入会话
- SignalR 冒烟 `tests/im-signalr-test.js`（9 项）：WebSocket 连接（query token）/ 实时双向收发 / 输入中 / 已读回执 / 非成员发送拒绝 / 重连补推离线消息
- 网关链路：`/api/im/**`、`/hub/chat/**` 经 8000 转发正常

## 七、后续增强（待 Phase 4）

- 在线状态订阅（好友/客服在线列表）；多端在线互踢策略
- 消息已读单条回执（目前整会话已读）；图片/文件上传（对象存储）
- 集群部署时连接管理器替换为 Redis（当前内存单机方案）；消息分库分表
- 接入 messaging-service 消息总线（目前订单/物流推送走内部 HTTP 接口）

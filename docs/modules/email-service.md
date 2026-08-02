# email-service — 自封装邮件微服务

> **所属阶段**：Phase 0 Week 3 · **优先级**：P0 · **端口**：8015
> **更新日期**：2026-08-02

## 一、职责

为平台提供统一的邮件发送能力（替代第三方邮件服务依赖）：

- 邮件发送：SMTP（MailKit），支持单发 / 批量
- 模板渲染：Razor 模板（RazorLight），支持 `@Model.xxx` 变量插值
- 发送状态追踪：待发送 / 成功 / 失败重试 / 死信
- 后台重试：指数退避（60s ×2，上限 10 分钟），超限转死信
- **DryRun 模式**：开发环境不真实发送（本地无 SMTP 服务器），仅记录日志

## 二、核心设计

```
┌──────────────────────────────────────────────┐
│             email-service (8015)              │
│                                              │
│  ┌──────────────┐  ┌──────────────────────┐  │
│  │ REST API      │  │ EmailRetryWorker     │  │
│  │ 发送/模板/查询 │  │ (BackgroundService)  │  │
│  └──────┬───────┘  │  指数退避重试 → 死信    │  │
│  ┌──────▼─────────▼──────────────────────┐  │
│  │     EmailSender + EmailTemplateRenderer│  │
│  │     SmtpSender (MailKit / DryRun)      │  │
│  └───────────────────────────────────────┘  │
│  ┌───────────────────────────────────────┐  │
│  │     SQL Server · MMP_Email 库          │  │
│  │  EmailMessage │ EmailTemplate         │  │
│  └───────────────────────────────────────┘  │
└──────────────────────────────────────────────┘
```

### 数据库（MMP_Email 库）

| 表 | 说明 | 关键索引 |
|----|------|---------|
| `EmailMessage` | 邮件记录（发送状态机） | `(Status, NextRetryTime)` 重试轮询；`To` 按收件人查询 |
| `EmailTemplate` | Razor 邮件模板 | `Name` 唯一 |

### 邮件状态机

```
Pending ──发送成功──▶ Sent
   │
   └──失败──▶ Failed ──指数退避重试──▶ Pending
                  └──超限──▶ DeadLetter ──手动重试──▶ Pending
```

## 三、REST API

### 邮件 `/api/emails`

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/emails` | 发送邮件（支持模板渲染） |
| POST | `/api/emails/batch` | 批量发送 |
| GET | `/api/emails?status=&to=&page=&pageSize=` | 分页查询 |
| GET | `/api/emails/{id}` | 按 Id 查询 |
| POST | `/api/emails/{id}/retry` | 手动重试（重置死信/失败） |
| POST | `/api/emails/{id}/deadletter` | 手动转死信 |

**发送请求体**：

```json
{
  "to": "buyer@example.com",
  "subject": "可选（用模板时由模板渲染）",
  "body": "可选（用模板时由模板渲染）",
  "templateName": "Welcome",
  "templateData": { "UserName": "Xiaoma" },
  "isHtml": true,
  "maxRetryCount": 3
}
```

### 模板 `/api/templates`

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/templates` | 创建模板 |
| GET | `/api/templates` | 模板列表 |
| GET | `/api/templates/{name}` | 按名称查询 |
| PUT | `/api/templates/{name}` | 更新模板 |
| POST | `/api/templates/{name}/activate` | 启用 |
| POST | `/api/templates/{name}/deactivate` | 停用 |
| DELETE | `/api/templates/{name}` | 删除 |

**模板示例（Razor）**：

```
subjectTemplate: Welcome @Model.UserName!
bodyTemplate:    <h1>Hi @Model.UserName,</h1><p>Welcome to MultiMerchant platform!</p>
```

### 网关入口（YARP）

```
/api/emails/**     → email-service (8015)
/api/templates/**  → email-service (8015)
/api/health/**     → email-service (8015)
```

## 四、配置说明（appsettings.json）

```json
{
  "ConnectionStrings": {
    "EmailDb": "Server=localhost;Database=MMP_Email;User Id=sa;Password=123456;TrustServerCertificate=True"
  },
  "Email": {
    "Host": "localhost",
    "Port": 25,
    "UseSsl": false,
    "Username": "",
    "Password": "",
    "DefaultFrom": "noreply@multimerchant.local",
    "DefaultFromName": "多商户平台",
    "DryRun": true,
    "RetryBaseIntervalSeconds": 60,
    "MaxRetryDelaySeconds": 600,
    "PollIntervalSeconds": 30
  }
}
```

> **生产切换**：将 `DryRun` 设为 `false` 并填写真实 SMTP（如腾讯云邮件 / 阿里云邮件），建议密码用环境变量注入。

## 五、项目结构

```
src/services/email-service/
├── Program.cs                        # 入口 + 启动自动迁移
├── Domain/Entities/                  # EmailMessage / EmailTemplate
├── Infrastructure/
│   ├── Persistence/EmailDbContext.cs
│   └── Mail/SmtpSender.cs            # MailKit + DryRun
├── Application/
│   ├── Options/EmailOptions.cs
│   ├── EmailTemplateRenderer.cs      # RazorLight 渲染
│   ├── EmailSender.cs                # 发送 + 状态机
│   ├── EmailRetryWorker.cs           # 后台重试
│   └── DependencyInjection.cs
├── DTOs/EmailDtos.cs
├── Controllers/                      # Emails / Templates / Health
└── Migrations/
```

## 六、已验证（冒烟测试，DryRun 模式）

| 场景 | 结果 |
|------|------|
| 健康检查（数据库连通） | ✅ healthy |
| 创建模板（Welcome） | ✅ |
| 模板渲染发送 | ✅ Subject=Welcome Xiaoma!，状态 Sent |
| 直接发送 | ✅ Order Shipped → Sent |
| 分页查询 | ✅ 记录完整 |

> 注：RazorLight 首次编译模板约需 10-20 秒（Roslyn 冷启动），后续走内存缓存。

## 七、已知限制与后续扩展

- **接收邮件**：IMAP 收件未实现（方案后续阶段按需）
- **消息队列集成**：当前发送失败由本地后台重试；后续可集成 messaging-service 做邮件任务队列解耦
- **附件**：当前未支持附件（后续 EmailMessage 增加附件路径/二进制列）
- **发送限流**：未做频率控制（防止批量误发），上线前需补充按收件域/账号限流
- **鉴权**：当前未加 JWT 认证，网关暴露公网时需补充

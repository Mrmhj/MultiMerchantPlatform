# 多商户入驻电商平台 — 项目规划方案 v4.1

> **文档路径**：`E:\MultiMerchantPlatform\docs\PROJECT_PLAN.md`
> **更新日期**：2026-08-02
> **版本**：v4.1（从 v4.0 调整而来）

---

## 版本变更记录

| 版本 | 日期 | 变更内容 |
|------|------|---------|
| v1.0 | 2026-08-01 | 初始方案：Java 21 + Spring Cloud Alibaba + 普通电商 |
| v2.0 | 2026-08-01 | 调整为 C#/.NET 8 + 多商户入驻平台 |
| v3.0 | 2026-08-01 | 调整为 .NET 10 + 自封装MQ/日志 + 多ORM切换 + BI平台 |
| v4.0 | 2026-08-01 | 移除Docker依赖 + 新增压测/IM/邮件服务 + 多端支持 + 全部E盘 + 文档管理策略 |
| **v4.1** | **2026-08-02** | **前端技术栈全面调整为 Vue 3 全家桶**（Vue3 + Vite + TS + Element Plus + uni-app + Electron） |

### v4.0 具体变更项

1. **移除 Docker 依赖** — Redis 改为 Windows 本地服务或 In-Memory 替代，所有中间件本地化运行
2. **新增 `performance-service`** — 压测 + 内存使用情况监控服务
3. **新增 `im-service`** — 即时通讯服务（SignalR WebSocket）
4. **新增 `email-service`** — 邮件发送/接收服务（SMTP/IMAP）
5. **新增多端支持** — Web（Next.js + Blazor）+ 移动端（.NET MAUI）+ 桌面端（.NET MAUI）
6. **全部 E 盘** — 代码 `E:\MultiMerchantPlatform\src\`，文档 `E:\MultiMerchantPlatform\docs\`
7. **文档管理策略** — 每次调整同步更新主文档 + 模块文档 + 变更记录

### v4.1 具体变更项

1. **Web 三端统一 Vue 3** — C端商城 / 商户端 / 平台管理后台全部改为 Vue 3.5 + Vite 8 + TypeScript 5.x + Element Plus 2.x（替代 Next.js 15 与 Blazor）
2. **移动端改为 uni-app** — Vue 3 语法一套代码编译 iOS + Android（替代 .NET MAUI）
3. **桌面端改为 Electron** — Vue 3 + WebView 打包 Windows + macOS 桌面应用（替代 .NET MAUI）
4. **前端统一技术栈收益** — 五个端共享一套组件库 / 请求封装 / 状态管理 / DTO 契约，维护成本最低
5. **配套库** — Pinia（状态）+ Vue Router 4（路由）+ Axios（请求）+ unplugin 自动导入 + ESLint + Prettier

---

## 一、环境检查结果（v4 更新）

| 组件 | 状态 | 详情 | 说明 |
|------|------|------|------|
| .NET SDK 10 | ✅ 已就绪 | 10.0.302 | 含 ASP.NET Core 10.0.10 |
| Node.js 24 | ✅ 已就绪 | v24.14.0 | 位于 `D:\Soft\nodejs\node.exe` |
| Git | ✅ 已就绪 | 2.54.0 | — |
| SQL Server | ✅ 已就绪 | SQL Server 2025 (17.0.1000.7) | 本地实例，sa/123456，库名 `MMP_*` |
| NuGet 源 | ✅ 已就绪 | nuget.org + VS Offline | — |
| Redis | ⚠️ 需安装 | 未检测到 | **不使用 Docker**，改为 Memurai（Redis Windows 兼容版）或 In-Memory |
| Docker | ❌ 不再需要 | v4 移除依赖 | 全部本地化运行 |
| E 盘空间 | ✅ 345GB 可用 | 358G 总容量 | 项目根目录 `E:\MultiMerchantPlatform\` |

### Redis 替代方案（无 Docker）

| 方案 | 说明 | 适用场景 |
|------|------|---------|
| **Memurai** | Redis 的 Windows 原生兼容版，免费开发版 | ⭐ 推荐，生产级兼容 |
| **In-Memory Cache** | .NET `IMemoryCache` + `IDistributedCache` | 开发阶段，单机足够 |
| **NCache** | .NET 原生分布式缓存 | 需要企业级分布式缓存时 |

> **决策**：开发阶段使用 In-Memory Cache，需要分布式缓存时安装 Memurai。

---

## 二、项目目录结构（E 盘）

```
E:\MultiMerchantPlatform\
│
├── docs/                              # 📂 所有项目文档
│   ├── PROJECT_PLAN.md                #   主方案文档（本文档）
│   ├── ARCHITECTURE.md                #   架构设计文档
│   ├── API_SPEC.md                    #   API 接口规范
│   ├── DATABASE.md                    #   数据库设计文档
│   ├── DEPLOYMENT.md                  #   部署与运维指南
│   ├── CHANGELOG.md                   #   变更记录（每次调整追加）
│   ├── DOC_INDEX.md                   #   文档索引（所有文档路径汇总）
│   └── modules/                       #   各模块详细文档
│       ├── identity-service.md
│       ├── merchant-service.md
│       ├── product-service.md
│       ├── order-service.md
│       ├── pay-service.md
│       ├── stock-service.md
│       ├── cart-service.md
│       ├── search-service.md
│       ├── promotion-service.md
│       ├── review-service.md
│       ├── logistics-service.md
│       ├── settlement-service.md
│       ├── messaging-service.md       #   自封装消息队列
│       ├── logging-service.md         #   自封装日志管理
│       ├── performance-service.md     #   压测+内存监控 (v4新增)
│       ├── im-service.md              #   即时通讯 (v4新增)
│       ├── email-service.md           #   邮件服务 (v4新增)
│       ├── risk-service.md
│       ├── notification-service.md
│       ├── bi-admin.md                #   BI分析平台
│       ├── web-customer.md            #   C端Web
│       ├── web-merchant.md            #   商户端Web
│       ├── web-admin.md               #   管理后台
│       ├── mobile-app.md              #   移动端 (v4新增)
│       └── desktop-app.md             #   桌面端 (v4新增)
│
├── src/                               # 📂 源代码
│   ├── BuildingBlocks/                #   公共基础组件（NuGet 本地包）
│   │   ├── BuildingBlocks.Core/        #     通用工具、枚举、基类
│   │   ├── BuildingBlocks.Data/        #     ORM 抽象层（EF/SqlSugar/Dapper 切换）
│   │   ├── BuildingBlocks.Communication/ #   服务间通信（HTTP/gRPC 切换）
│   │   ├── BuildingBlocks.Messaging/   #     消息队列客户端 SDK
│   │   ├── BuildingBlocks.Logging/     #     日志客户端 SDK
│   │   ├── BuildingBlocks.Security/    #     认证授权、JWT、权限
│   │   ├── BuildingBlocks.Cache/       #     缓存抽象（In-Memory / Redis）
│   │   └── BuildingBlocks.MultiTenant/ #     多租户/多商户基础
│   │
│   ├── services/                      #   微服务
│   │   ├── identity-service/          #     P0 - 用户认证
│   │   ├── merchant-service/          #     P0 - 商户管理
│   │   ├── product-service/           #     P0 - 商品管理
│   │   ├── order-service/             #     P0 - 订单管理
│   │   ├── pay-service/               #     P0 - 支付服务
│   │   ├── stock-service/             #     P0 - 库存管理
│   │   ├── cart-service/              #     P1 - 购物车
│   │   ├── search-service/            #     P1 - 搜索服务
│   │   ├── promotion-service/         #     P1 - 促销/优惠券
│   │   ├── review-service/            #     P1 - 评价
│   │   ├── logistics-service/         #     P1 - 物流
│   │   ├── settlement-service/        #     P1 - 结算分账
│   │   ├── messaging-service/         #     P0 - 自封装消息队列
│   │   ├── logging-service/           #     P0 - 自封装日志管理
│   │   ├── performance-service/       #     P2 - 压测+内存监控 (v4新增)
│   │   ├── im-service/                #     P2 - 即时通讯 (v4新增)
│   │   ├── email-service/             #     P1 - 邮件服务 (v4新增)
│   │   ├── risk-service/              #     P2 - 风控
│   │   ├── notification-service/      #     P2 - 通知中心
│   │   └── bi-admin/                  #     P3 - BI分析平台
│   │
│   ├── gateways/                      #   API 网关
│   │   └── api-gateway/               #     YARP 反向代理网关
│   │
│   ├── apps/                          #   前端应用（v4.1 统一 Vue 3）
│   │   ├── shared/                    #     共享代码（DTO / API 客户端 / 组件）
│   │   ├── web-customer/              #     C端电商 (Vue 3 + Vite + Element Plus)
│   │   ├── web-merchant/              #     商户端 (Vue 3 + Vite + Element Plus)
│   │   ├── web-admin/                 #     平台管理后台 (Vue 3 + Vite + Element Plus)
│   │   ├── mobile-app/                #     移动端 uni-app (v4.1改) — Vue 3 语法，iOS + Android
│   │   └── desktop-app/               #     桌面端 Electron (v4.1改) — Windows + macOS
│   │
│   └── AspireHost/                    #   .NET Aspire 编排宿主
│       └── AspireHost.AppHost/        #     启动入口，编排所有服务
│
├── tests/                             # 📂 测试项目
│   ├── unit/                          #   单元测试
│   ├── integration/                   #   集成测试
│   └── load/                          #   压测脚本
│
├── scripts/                           # 📂 脚本工具
│   ├── init-db.sql                    #   数据库初始化脚本
│   ├── seed-data.sql                  #   种子数据
│   └── deploy/                        #   部署脚本
│
├── .gitignore
├── .editorconfig
├── Directory.Build.props              #   全局编译属性
├── Directory.Packages.props           #   全局 NuGet 包版本管理（CPM）
└── README.md
```

---

## 三、技术选型（v4 调整）

### 后端技术栈

| 层 | 技术 | 版本 | 说明 |
|----|------|------|------|
| 运行时 | .NET | 10.0 | SDK 10.0.302 已就绪 |
| Web 框架 | ASP.NET Core | 10.0 | Minimal API + Controller 混合 |
| 微服务编排 | .NET Aspire | 10.0 | 本地开发编排，无 Docker |
| API 网关 | YARP | 2.0 | 微软官方反向代理 |
| ORM | EF Core 10 / SqlSugar / Dapper | 可切换 | `BuildingBlocks.Data` 统一抽象 |
| 数据库 | SQL Server | 2025 | 本机实例（sa 账户），库名前缀 `MMP_` |
| 缓存 | IMemoryCache / Memurai | — | 无 Docker，开发用 In-Memory |
| 消息队列 | **自封装 messaging-service** | — | SQL Server 持久化，不依赖 RabbitMQ |
| 日志 | **自封装 logging-service** | — | 不依赖 Seq/ELK |
| 分布式事务 | MassTransit Saga | 8.x | 状态机编排 |
| 限流熔断 | Polly | 8.x | + .NET RateLimiter |
| 链路追踪 | OpenTelemetry | 1.x | + Jaeger（本地安装） |
| 实时通信 | SignalR | 10.0 | IM 服务 + 通知推送 |
| 邮件 | MailKit | 4.x | SMTP 发送 + IMAP 接收 |

### 前端技术栈（v4.1 全面调整）

| 端 | 技术 | 版本 | 说明 |
|----|------|------|------|
| 全端基础 | Vue 3（Composition API + `<script setup>`） | 3.5.x | 五端统一框架 |
| 构建工具 | Vite | 8.x | 极速 HMR，Node 24 已满足 |
| 类型安全 | TypeScript | 5.x | 严格模式 |
| UI 组件库 | Element Plus | 2.x | 统一五端 UI（C端可 CSS 变量换肤） |
| 状态管理 | Pinia | 3.x | 五端统一 |
| 路由 | Vue Router | 4.x | Web 三端 |
| HTTP 客户端 | Axios（统一封装 + 拦截器） | 1.x | JWT 注入 / 401 刷新 / 错误统一处理 |
| 按需加载 | unplugin-auto-import + unplugin-vue-components | — | Element Plus 自动按需导入 |
| 代码规范 | ESLint 9 + Prettier | — | 统一风格 |
| C端 Web | Vue 3 + Vite + TS + Element Plus | — | 消费者商城 |
| 商户端 Web | Vue 3 + Vite + TS + Element Plus | — | 商户管理后台 |
| 平台管理后台 | Vue 3 + Vite + TS + Element Plus | — | 平台运营/审核/风控（原 Blazor 取消） |
| 移动端 | uni-app（Vue 3 语法） | 4.x | iOS + Android 一套代码 |
| 桌面端 | Electron + Vue 3 | 33.x | Windows + macOS 商户工作台 |
| 多端共享 | `apps/shared/` | — | 共享 DTO、API 客户端、组件、Pinia store |

### 多端架构设计（v4.1）

```
                    ┌─────────────────────────────────┐
                    │         API Gateway (YARP)       │
                    │    /api/c/* → C端接口              │
                    │    /api/m/* → 商户端接口            │
                    │    /api/a/* → 管理后台接口           │
                    │    /hub/*  → SignalR Hub           │
                    └──────────┬──────────────────────┘
                               │
          ┌──────────┬─────────┼──────────┬──────────┐
          │          │         │          │          │
     ┌────▼───┐ ┌───▼────┐ ┌──▼───┐ ┌───▼────┐ ┌──▼─────┐
     │ C端Web │ │商户端Web│ │管理后台│ │ 移动端  │ │ 桌面端  │
     │  Vue3  │ │  Vue3  │ │ Vue3 │ │ uni-app│ │Electron│
     │(浏览器) │ │(浏览器) │ │(浏览器)│ │(iOS/安卓)│ │(Win/Mac)│
     └────────┘ └────────┘ └──────┘ └────────┘ └────────┘
```

**多端共享策略**：
- API 层统一：所有端通过同一个 YARP 网关访问后端微服务
- DTO 契约共享：`BuildingBlocks.Core` 中定义所有 DTO，前端通过 OpenAPI 自动生成 TS 客户端（`apps/shared/api/`）
- 认证统一：JWT Token，所有端共享同一套身份认证
- SignalR 多端推送：Web 用 `@microsoft/signalr`，uni-app 用 `@microsoft/signalr`（H5）+ 原生封装（App），Electron 用 `@microsoft/signalr`

---

## 四、微服务拆分（v4 — 共 21 个服务）

### 服务全景图

| 优先级 | 服务名 | 说明 | 端口 |
|--------|--------|------|------|
| **P0** | identity-service | 用户注册/登录/JWT/OAuth | 8001 |
| **P0** | merchant-service | 商户入驻/审核/店铺管理 | 8002 |
| **P0** | product-service | 商品 CRUD/SKU/分类 | 8003 |
| **P0** | order-service | 订单创建/拆单/状态机 | 8004 |
| **P0** | pay-service | 支付网关/回调/退款 | 8005 |
| **P0** | stock-service | 库存扣减/预占/回滚 | 8006 |
| **P0** | messaging-service | 自封装消息队列 | 8010 |
| **P0** | logging-service | 自封装日志管理 | 8011 |
| **P1** | cart-service | 购物车 | 8007 |
| **P1** | search-service | 商品搜索（SQL Full-Text） | 8008 |
| **P1** | promotion-service | 优惠券/满减/活动 | 8009 |
| **P1** | review-service | 商品评价 | 8012 |
| **P1** | logistics-service | 物流对接/追踪 | 8013 |
| **P1** | settlement-service | 佣金计算/结算分账 | 8014 |
| **P1** | email-service | 邮件发送/接收 **(v4新增)** | 8015 |
| **P2** | im-service | 即时通讯 **(v4新增)** | 8016 |
| **P2** | performance-service | 压测+内存监控 **(v4新增)** | 8017 |
| **P2** | risk-service | 风控/反刷单 | 8018 |
| **P2** | notification-service | 通知中心（短信/站内信/Push） | 8019 |
| **P3** | bi-admin | BI 分析管理平台 | 8020 |
| — | api-gateway | YARP 网关 | 8000 |

### v4 新增服务详细设计

#### 1. performance-service（压测 + 内存监控）

**职责**：
- 对任意微服务发起压力测试（HTTP 并发请求模拟）
- 实时监控所有微服务的内存/CPU/GC/线程池指标
- 生成压测报告 + 性能瓶颈分析
- 异常指标自动告警（通过 notification-service 推送）

**技术方案**：
```
┌──────────────────────────────────────────┐
│           performance-service             │
│                                          │
│  ┌─────────────┐   ┌──────────────────┐ │
│  │  压测引擎     │   │  监控采集器        │ │
│  │  (BenchmarkDotNet││  (EventCounters  │ │
│  │   + HttpClient) ││   + diagnostic)  │ │
│  └──────┬──────┘   └────────┬─────────┘ │
│         │                   │           │
│  ┌──────▼──────┐   ┌────────▼─────────┐ │
│  │  压测报告生成 │   │  指标存储(SQL)    │ │
│  │  (HTML/PDF)  │   │  + 告警判断       │ │
│  └─────────────┘   └──────────────────┘ │
│                                          │
│  ┌─────────────────────────────────────┐ │
│  │  Vue 3 + ECharts 看板（内存/性能）   │ │
│  └─────────────────────────────────────┘ │
└──────────────────────────────────────────┘
```

**监控指标**：
| 指标 | 来源 | 说明 |
|------|------|------|
| 内存使用 | `GC.GetTotalMemory()` + `Process.WorkingSet64` | 托管 + 非托管 |
| GC 统计 | `EventCounter` "gc-heap-size"、"gen-0-gc-count" 等 | 各代 GC 频率和耗时 |
| CPU 使用率 | `Process.TotalProcessorTime` | 按服务统计 |
| 线程池 | `ThreadPool.GetAvailableThreads()` | 可用工作线程 + IO 线程 |
| HTTP 请求 | YARP 统计 | QPS / 延迟 / 错误率 |
| 数据库连接 | EF Core `DbContext` 池统计 | 活跃连接数 |

**压测功能**：
- 配置压测目标：URL / 方法 / 并发数 / 持续时间 / 请求体
- 实时显示：QPS / 平均延迟 / P99 延迟 / 错误率
- 压测完成后生成报告（HTML + PDF），保存到 `E:\MultiMerchantPlatform\docs\reports\`

#### 2. im-service（即时通讯）

**职责**：
- 用户间私聊（C端买家 ↔ 卖家客服）
- 商户客服群聊
- 订单状态实时推送
- 物流状态实时推送
- 系统通知推送
- 离线消息存储 + 上线推送

**技术方案**：
```
┌──────────────────────────────────────────┐
│              im-service                   │
│                                          │
│  ┌─────────────┐   ┌──────────────────┐ │
│  │ SignalR Hub  │   │  消息存储          │ │
│  │ (WebSocket)  │   │  (SQL Server)     │ │
│  └──────┬──────┘   └────────┬─────────┘ │
│         │                   │           │
│  ┌──────▼──────┐   ┌────────▼─────────┐ │
│  │ 连接管理器    │   │  消息分发          │ │
│  │ (用户↔连接映射)│   │  (私聊/群聊/广播)  │ │
│  └─────────────┘   └──────────────────┘ │
│                                          │
│  ┌─────────────────────────────────────┐ │
│  │  消息类型：文本/图片/文件/订单卡片/系统  │ │
│  └─────────────────────────────────────┘ │
└──────────────────────────────────────────┘
```

**SignalR Hub 设计**：
```csharp
[Authorize]
public class ChatHub : Hub
{
    // 用户上线 → 建立连接映射 → 推送离线消息
    public override async Task OnConnectedAsync() { }
    
    // 发送私聊消息
    public async Task SendPrivateMessage(Guid toUserId, string content) { }
    
    // 发送群聊消息
    public async Task SendGroupMessage(Guid groupId, string content) { }
    
    // 输入中状态
    public async Task SendTypingIndicator(Guid toUserId) { }
    
    // 已读回执
    public async Task MarkAsRead(Guid messageId) { }
}
```

**多端接入**：
- Web / Electron 端：`@microsoft/signalr` npm 包
- uni-app 端：`@microsoft/signalr`（H5/小程序）+ 原生 WebSocket 封装（App 端）

#### 3. email-service（邮件服务）

**职责**：
- 发送邮件：注册验证码、订单确认、发货通知、营销邮件
- 接收邮件：商户客服邮箱收件（IMAP 拉取）
- 邮件模板管理（Razor 模板渲染）
- 邮件队列（通过 messaging-service 异步发送）
- 发送状态追踪（已发送/已读/失败重试）

**技术方案**：
```
┌──────────────────────────────────────────┐
│              email-service                │
│                                          │
│  ┌─────────────┐   ┌──────────────────┐ │
│  │ SMTP 发送器   │   │  IMAP 接收器      │ │
│  │ (MailKit)    │   │  (MailKit)       │ │
│  └──────┬──────┘   └────────┬─────────┘ │
│         │                   │           │
│  ┌──────▼──────┐   ┌────────▼─────────┐ │
│  │ 模板引擎      │   │  邮件队列          │ │
│  │ (Razor Light)│   │  (messaging-svc) │ │
│  └─────────────┘   └──────────────────┘ │
│                                          │
│  ┌─────────────────────────────────────┐ │
│  │ 邮件日志 + 状态追踪 + 失败重试          │ │
│  └─────────────────────────────────────┘ │
└──────────────────────────────────────────┘
```

**邮件模板**：
| 模板名 | 触发场景 | 变量 |
|--------|---------|------|
| `Welcome` | 用户注册成功 | 用户名、验证链接 |
| `VerificationCode` | 邮箱验证 | 验证码（6位） |
| `OrderConfirmed` | 订单支付成功 | 订单号、金额、商品列表 |
| `OrderShipped` | 商家发货 | 订单号、物流单号、物流公司 |
| `PasswordReset` | 密码重置 | 重置链接 |
| `MerchantApproved` | 商户入驻审核通过 | 商户名、登录链接 |
| `MarketingPromo` | 营销活动 | 活动名称、优惠券链接 |

---

## 五、ORM 多框架切换设计

### 架构设计

```
┌──────────────────────────────────────────────┐
│              业务服务层                        │
│   (只依赖 IRepository<T> 接口)                 │
└──────────────────┬───────────────────────────┘
                   │
┌──────────────────▼───────────────────────────┐
│         BuildingBlocks.Data                   │
│  ┌─────────────────────────────────────────┐ │
│  │  IRepository<T>  ← 统一接口               │ │
│  ├─────────────────────────────────────────┤ │
│  │  EfRepository    │ SqlSugarRepository   │ │
│  │  (EF Core 10)    │ (SqlSugar)           │ │
│  ├─────────────────────────────────────────┤ │
│  │  DapperRepository │  IDbConnectionSwitcher│ │
│  │  (Dapper)         │  (多数据库连接切换)    │ │
│  └─────────────────────────────────────────┘ │
└──────────────────┬───────────────────────────┘
                   │
┌──────────────────▼───────────────────────────┐
│              SQL Server                       │
│   主库 │ 商户库 │ 订单库 │ 日志库 │ ...        │
└──────────────────────────────────────────────┘
```

### 配置切换

```json
// appsettings.json
{
  "Data": {
    "DefaultOrm": "EfCore",  // "EfCore" | "SqlSugar" | "Dapper"
    "Connections": {
      "Default": "Server=localhost;Database=MMP_Main;User Id=sa;Password=123456;TrustServerCertificate=True",
      "Order": "Server=localhost;Database=MMP_Order;User Id=sa;Password=123456;TrustServerCertificate=True",
      "ExternalERP": "Server=192.168.1.100;Database=ERP;User Id=sa;Password=xxx"
    }
  }
}
```

```csharp
// 注册时切换
services.AddDataLayer(config => {
    config.UseEfCore();      // 或 config.UseSqlSugar(); 或 config.UseDapper();
    config.AddConnection("Default", connectionString);
    config.AddConnection("Order", orderConnStr);
    config.AddConnection("ExternalERP", erpConnStr);  // 外部系统连接
});

// 业务代码中使用
public class OrderService
{
    private readonly IRepository<Order> _repo;
    private readonly IDbConnectionSwitcher _dbSwitcher;
    
    public async Task<List<Order>> GetOrdersAsync()
    {
        // 使用默认 ORM
        return await _repo.ToListAsync();
    }
    
    public async Task CallExternalErpSpAsync()
    {
        // 切换到外部系统数据库连接，调用存储过程
        using var conn = _dbSwitcher.GetConnection("ExternalERP");
        var result = await conn.QueryAsync<OrderDto>("sp_GetOrders", 
            commandType: CommandType.StoredProcedure);
        return result;
    }
}
```

---

## 六、自封装基础设施服务

### messaging-service（消息队列微服务）

**设计文档**：`E:\MultiMerchantPlatform\docs\modules\messaging-service.md`

| 特性 | 实现方式 |
|------|---------|
| 持久化 | SQL Server 表存储消息 |
| 分发机制 | `BackgroundService` 轮询 + 乐观锁抢占 |
| 重试 | 指数退避（1s → 2s → 4s → ... → 最大 60s） |
| 死信队列 | 超过最大重试次数 → DeadLetter 表 + 告警 |
| 幂等 | 消费者返回幂等 Key，去重表校验 |
| 顺序消费 | 同一聚合根 ID 路由到同一队列 |
| 传输层切换 | In-Memory（开发）/ SQL Server（默认）/ RabbitMQ（可选） |

### logging-service（日志管理微服务）

**设计文档**：`E:\MultiMerchantPlatform\docs\modules\logging-service.md`

| 特性 | 实现方式 |
|------|---------|
| 采集 | `ILoggerProvider` → 批量异步上报 |
| 存储 | SQL Server，按月分表 |
| 查询 | REST API 按服务/级别/时间/关键词查询 |
| 告警 | Error 级别日志自动触发 notification-service |
| 归档 | 超过 90 天的日志压缩归档到文件 |
| 集成 | NuGet 包 `AddCentralizedLogging()` 一行接入 |

---

## 七、核心业务流程

### 多商户交易闭环

```
用户浏览商品 → 加入购物车 → 创建订单
                                │
                    ┌───────────┴───────────┐
                    │   order-service        │
                    │   按商户拆分为子订单     │
                    └───────────┬───────────┘
                                │
              ┌─────────────────┼─────────────────┐
              │                 │                 │
     ┌────────▼───┐    ┌───────▼────┐    ┌──────▼─────┐
     │ 商户A 子订单 │    │ 商户B 子订单 │    │ 商户C 子订单 │
     └────────┬───┘    └───────┬────┘    └──────┬─────┘
              │                 │                 │
              └─────────────────┼─────────────────┘
                                │
                    ┌───────────▼───────────┐
                    │   stock-service        │
                    │   各商户库存分别扣减     │
                    └───────────┬───────────┘
                                │
                    ┌───────────▼───────────┐
                    │   pay-service          │
                    │   统一支付 → 分账       │
                    └───────────┬───────────┘
                                │
                    ┌───────────▼───────────┐
                    │   logistics-service    │
                    │   各子订单独立发货       │
                    └───────────┬───────────┘
                                │
                    ┌───────────▼───────────┐
                    │   settlement-service   │
                    │   佣金计算 → 商户结算   │
                    └───────────────────────┘
```

### 秒杀流程（高并发场景）

```
用户点击秒杀
    │
    ▼
[1] In-Memory Cache 预扣库存 → 原子操作 Decrement
    │ 库存不足 → 直接返回"已售罄"
    ▼
[2] 发送消息到 messaging-service → 异步创建订单
    │
    ▼
[3] order-service 消费消息 → 创建订单 → 写入数据库
    │
    ▼
[4] 返回订单号 → 用户跳转支付页
    │ 15分钟未支付 → messaging-service 延迟消息 → 回滚库存
    ▼
[5] 支付成功 → 更新订单状态 → 推送 IM 通知 → 发送邮件
```

---

## 八、数据库设计概要

### 数据库实例划分

| 数据库 | 服务 | 说明 |
|--------|------|------|
| MMP_Main | identity, merchant, promotion | 用户、商户、营销 |
| MMP_Product | product, stock, search | 商品、库存 |
| MMP_Order | order, cart, settlement | 订单、购物车、结算 |
| MMP_Pay | pay | 支付流水 |
| MMP_Logistics | logistics, review | 物流、评价 |
| MMP_Infra | messaging, logging, performance | 基础设施 |
| MMP_Im | im-service | 即时通讯消息 |
| MMP_Email | email-service | 邮件记录 |
| MMP_BI | bi-admin | BI 分析（只读副本 + 聚合表） |

### 多商户数据隔离

```csharp
// EF Core 全局查询过滤器
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasQueryFilter(p => p.MerchantId == _tenantProvider.CurrentMerchantId);
        builder.HasIndex(p => new { p.MerchantId, p.Sku });
    }
}
```

---

## 九、多端应用设计（v4.1 全面调整）

### 移动端（uni-app — Vue 3 语法）

> **框架**：uni-app 4.x（Vue 3 + Vite 编译），一套代码编译 iOS / Android，`apps/mobile-app/`

| 功能模块 | 说明 |
|---------|------|
| 首页 | 轮播图 + 商品推荐 + 分类入口 |
| 搜索 | 搜索栏 + 历史搜索 + 热门搜索 |
| 商品详情 | SKU 选择 + 评价 + 详情页 |
| 购物车 | 多商户分组 + 全选/单选 |
| 订单 | 订单列表 + 详情 + 物流追踪 |
| 支付 | 微信/支付宝调用（uni 原生支付 API） |
| IM 聊天 | 客服聊天列表 + 聊天界面（SignalR） |
| 个人中心 | 订单状态入口 + 收藏 + 设置 |

### 桌面端（Electron + Vue 3）

> **框架**：Electron 33.x + Vue 3 + Vite + Element Plus，`apps/desktop-app/`，主进程 + 渲染进程分离

| 功能模块 | 说明 |
|---------|------|
| 商户工作台 | 订单管理 + 商品管理 + 数据概览 |
| 实时消息 | 客服聊天 + 订单通知（SignalR） |
| 数据报表 | 销售统计 + 库存报表（ECharts） |
| 系统监控 | performance-service 看板（管理员） |
| 本地能力 | 文件导出 / 打印 / 系统托盘（Electron main 进程） |

### 多端代码共享

```
src/apps/
├── shared/                    # 多端共享代码（npm 包 or pnpm workspace）
│   ├── api/                   #   OpenAPI 生成的 TS 客户端（DTO + Axios）
│   ├── components/            #   共享 Vue 组件（Element Plus 二次封装）
│   ├── stores/                #   共享 Pinia store
│   ├── composables/           #   共享组合式函数（useAuth / useCart / useSocket）
│   └── styles/                #   主题变量（CSS 变量换肤）
├── web-customer/              # C端商城（Vue 3 + Vite + TS + Element Plus）
│   ├── src/
│   │   ├── views/             #   页面（首页/详情/购物车/订单/支付）
│   │   ├── router/
│   │   └── main.ts
├── web-merchant/              # 商户端（Vue 3 + Vite + TS + Element Plus）
├── web-admin/                 # 平台管理后台（Vue 3 + Vite + TS + Element Plus）
├── mobile-app/                # 移动端 uni-app
│   ├── src/
│   │   ├── pages/             #   页面（uni-app 路由）
│   │   └── manifest.json      #   App 配置（iOS/Android）
└── desktop-app/               # 桌面端 Electron
    ├── electron/
    │   ├── main/              #   主进程（窗口/托盘/本地文件）
    │   └── preload/           #   预加载脚本
    └── src/                   #   渲染进程（Vue 3 应用）
```

---

## 十、落地路线图（v4 — 22 周）

### Phase 0：基础设施（第 1-3 周）

| 周 | 任务 | 产出 |
|----|------|------|
| 1 | GitHub 建仓 + E 盘目录初始化 + BuildingBlocks 骨架 | 仓库结构 + 编译通过 |
| 1 | AspireHost 编排骨架 + YARP 网关基础 | 服务可启动 |
| 2 | **messaging-service** 开发 | 消息队列可用 |
| 2 | **logging-service** 开发 | 日志采集可用 |
| 3 | **email-service** 开发 | 邮件发送可用 |
| 3 | BuildingBlocks.Data（ORM 切换）+ BuildingBlocks.Communication | 多 ORM + HTTP/gRPC 可切换 |

### Phase 1：核心交易闭环（第 4-9 周）

| 周 | 任务 | 产出 |
|----|------|------|
| 4 | identity-service（注册/登录/JWT） | 用户系统 |
| 4-5 | merchant-service（入驻/审核/店铺） | 商户系统 |
| 5-6 | product-service（商品/SKU/分类） | 商品系统 |
| 6-7 | order-service（创建/拆单/状态机） | 订单系统 |
| 7-8 | pay-service（支付/回调/退款） | 支付系统 |
| 8 | stock-service（扣减/预占/回滚） | 库存系统 |
| 9 | C端 Web 前端（Vue 3 + Vite + Element Plus） | C端商城可浏览下单 |

### Phase 2：辅助功能 + 多端（第 10-13 周）

| 周 | 任务 | 产出 |
|----|------|------|
| 10 | cart-service + search-service | 购物车 + 搜索 |
| 10-11 | promotion-service + review-service | 优惠券 + 评价 |
| 11 | logistics-service + settlement-service | 物流 + 结算 |
| 12 | **im-service**（即时通讯） | 聊天功能 |
| 12-13 | 商户端 Web 前端（Vue 3） | 商户管理后台 |
| 13 | **移动端 uni-app** 骨架 + 核心页面 | App 可运行 |

### Phase 3：平台支撑（第 14-16 周）

| 周 | 任务 | 产出 |
|----|------|------|
| 14 | **performance-service**（压测+内存监控） | 监控看板可用 |
| 14 | risk-service（风控/反刷单） | 风控规则引擎 |
| 15 | notification-service（通知中心） | 短信/站内信/Push |
| 15-16 | **桌面端 Electron** | 桌面应用可运行 |
| 16 | **BI 分析管理平台**（Vue 3 + ECharts） | BI 看板 |

### Phase 4：高并发优化 + 压测（第 17-19 周）

| 周 | 任务 | 产出 |
|----|------|------|
| 17 | 秒杀场景实现（缓存预扣 + 异步下单） | 秒杀功能 |
| 18 | 缓存策略优化 + 数据库分库分表 | 性能提升 |
| 19 | **performance-service 全量压测** + 瓶颈优化 | 压测报告 |

### Phase 5：部署上线（第 20-22 周）

| 周 | 任务 | 产出 |
|----|------|------|
| 20 | K8s 清单编写（可选）或 Windows Service 部署 | 部署方案 |
| 21 | 监控告警完善 + 日志归档 + 链路追踪 | 运维体系 |
| 22 | 灰度发布 + 全量上线 | 正式上线 |

---

## 十一、文档管理策略（v4 新增）

### 文档体系

```
E:\MultiMerchantPlatform\docs\
├── PROJECT_PLAN.md            # 主方案（本文档）— 每次调整更新
├── ARCHITECTURE.md            # 架构设计 — 技术变更时更新
├── API_SPEC.md                # API 规范 — 接口变更时更新
├── DATABASE.md                # 数据库设计 — 表结构变更时更新
├── DEPLOYMENT.md              # 部署指南 — 部署方式变更时更新
├── CHANGELOG.md               # 变更记录 — 每次调整追加记录
├── DOC_INDEX.md               # 文档索引 — 新增文档时更新
├── modules/                   # 各模块详细文档 — 模块变更时更新
│   ├── identity-service.md
│   ├── im-service.md
│   └── ...
└── reports/                   # 压测报告/分析报告（自动生成）
    ├── loadtest-2026-08-15.html
    └── ...
```

### 文档更新规则

| 触发事件 | 需更新的文档 |
|---------|-------------|
| 方案调整（如本次） | PROJECT_PLAN.md + CHANGELOG.md + 受影响的模块文档 |
| 新增/删除服务 | PROJECT_PLAN.md + DOC_INDEX.md + modules/ 下新增/删除文档 |
| 技术栈变更 | PROJECT_PLAN.md + ARCHITECTURE.md + CHANGELOG.md |
| API 变更 | API_SPEC.md + 对应模块文档 |
| 数据库变更 | DATABASE.md + 对应模块文档 |
| 压测完成 | reports/ 下自动生成报告 |

### CHANGELOG.md 格式

```markdown
## [v4.1] - 2026-08-02

### Changed
- 前端技术栈全面调整为 Vue 3 全家桶：
  - C端/商户端/管理后台：Next.js 15 / Blazor → Vue 3.5 + Vite 8 + TS 5.x + Element Plus 2.x
  - 移动端：.NET MAUI → uni-app（Vue 3 语法，iOS + Android）
  - 桌面端：.NET MAUI → Electron + Vue 3
- 新增 apps/shared 共享层（OpenAPI TS 客户端 / 组件 / store / composables）

## [v4.0] - 2026-08-01

### Added
- 新增 performance-service（压测+内存监控）
- 新增 im-service（即时通讯）
- 新增 email-service（邮件服务）
- 新增多端支持（移动端 + 桌面端）
- 新增文档管理策略

### Changed
- 移除 Docker 依赖，Redis 改为 Memurai/In-Memory
- 项目路径从工作区改为 E:\MultiMerchantPlatform\
- 落地路线从 18 周调整为 22 周

### Removed
- Docker 依赖
- Docker Compose 配置
```

### DOC_INDEX.md 格式

```markdown
# 文档索引

## 主文档
| 文档 | 路径 | 说明 |
|------|------|------|
| 项目方案 | docs/PROJECT_PLAN.md | 总体规划 |
| 架构设计 | docs/ARCHITECTURE.md | 技术架构 |
| ... | ... | ... |

## 模块文档
| 模块 | 路径 | 状态 |
|------|------|------|
| identity-service | docs/modules/identity-service.md | ✅ 已完成 |
| im-service | docs/modules/im-service.md | 📝 待编写 |
| ... | ... | ... |
```

---

## 十二、GitHub 仓库结构

```bash
# 仓库结构（与 E 盘项目目录一致）
MultiMerchantPlatform/
├── docs/                    # 文档
├── src/                     # 源代码
│   ├── BuildingBlocks/
│   ├── services/
│   ├── gateways/
│   ├── apps/
│   └── AspireHost/
├── tests/
├── scripts/
├── .gitignore
├── Directory.Build.props
├── Directory.Packages.props
└── README.md
```

### 分支策略

| 分支 | 用途 |
|------|------|
| `main` | 生产稳定版本 |
| `dev` | 开发集成分支 |
| `feature/*` | 功能开发分支 |
| `fix/*` | Bug 修复分支 |
| `release/*` | 发布准备分支 |

---

## 十三、AI 智能体协作开发指南

### 与 AI 协作的最佳实践

1. **提供完整上下文**：每次让 AI 写代码前，提供相关模块文档路径 + 已有代码结构
2. **小步快跑**：一次只实现一个服务的某个功能模块
3. **先骨架后血肉**：先搭建服务骨架（Program.cs + 基础配置），再填充业务逻辑
4. **同步更新文档**：每个功能完成后，更新对应模块文档
5. **Code Review**：AI 写完后，人工审查 + 跑通测试

### 典型 Prompt 示例

```
我正在开发 E:\MultiMerchantPlatform 下的多商户电商平台。
当前需要实现 im-service（即时通讯服务）。

请先阅读以下文档：
- E:\MultiMerchantPlatform\docs\PROJECT_PLAN.md（第4节 im-service 部分）
- E:\MultiMerchantPlatform\docs\modules\im-service.md（如已有）

技术要求：
- .NET 10 + SignalR
- 依赖 BuildingBlocks.Messaging 和 BuildingBlocks.Logging
- 数据库 MMP_Im
- 通过 AspireHost 编排

请帮我实现：
1. im-service 的项目骨架（Program.cs + 基础配置）
2. ChatHub 的核心方法
3. 消息存储逻辑
4. 对应的单元测试
```

---

## 下一步

1. **确认 v4 方案** — 如方案 OK，开始 Phase 0 执行
2. **继续调整** — 如有修改意见，告诉我具体改什么
3. **直接开干** — 我可以立刻：
   - 在 E 盘初始化 Git 仓库
   - 创建 GitHub 远程仓库
   - 搭建 BuildingBlocks 骨架
   - 创建 AspireHost 编排宿主

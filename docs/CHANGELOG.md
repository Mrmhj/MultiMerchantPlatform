# 变更记录

## [v4.1] - 2026-08-02

### Changed
- 前端技术栈全面调整为 **Vue 3 全家桶**（用户确认，替代 Next.js 15 + Blazor + MAUI）：
  - C端 Web / 商户端 Web / 平台管理后台：**Vue 3.5 + Vite 8 + TypeScript 5.x + Element Plus 2.x**（原 Next.js 15 / Blazor + MudBlazor）
  - 移动端：**.NET MAUI → uni-app**（Vue 3 语法，一套代码编译 iOS + Android）
  - 桌面端：**.NET MAUI → Electron + Vue 3**（Windows + macOS 商户工作台）
- 统一配套库：Pinia 状态管理 + Vue Router 4 + Axios 请求封装 + unplugin 按需自动导入 + ESLint/Prettier
- 新增 `apps/shared/` 前端共享层：OpenAPI 自动生成 TS 客户端 / 共享组件 / Pinia store / composables
- performance-service 看板与 BI 平台改 Vue 3 + ECharts（原 Blazor）

---

## [v4.0] - 2026-08-01

### Added
- 新增 `performance-service`：压测引擎 + 内存/CPU/GC/线程池监控 + Blazor 看板 + 压测报告生成
- 新增 `im-service`：基于 SignalR 的即时通讯，支持私聊/群聊/订单推送/离线消息
- 新增 `email-service`：MailKit SMTP 发送 + IMAP 接收 + Razor 模板渲染 + 邮件队列
- 新增多端支持：移动端（.NET MAUI iOS/Android）+ 桌面端（.NET MAUI Windows/macOS）
- 新增文档管理策略：主文档 + 模块文档 + 变更记录 + 文档索引
- 新增 `E:\MultiMerchantPlatform\` 项目根目录及完整目录结构
- 新增 `docs/reports/` 目录用于存放压测报告

### Changed
- 移除 Docker 依赖，Redis 改为 Memurai（Windows 原生 Redis）或 In-Memory Cache
- 项目路径从 `C:\Users\15123\WorkBuddy\` 工作区改为 `E:\MultiMerchantPlatform\`
- 微服务数量从 16 个增加到 21 个（新增 performance/im/email + 保留原有）
- 落地路线从 18 周调整为 22 周（5 个 Phase）
- Phase 0 基础设施阶段增加 email-service 开发
- Phase 2 增加 im-service 和移动端 MAUI 骨架
- Phase 3 增加 performance-service 和桌面端 MAUI
- Phase 4 增加 performance-service 全量压测
- `BuildingBlocks.Cache` 改为支持 In-Memory / Memurai 双模式

### Removed
- Docker 依赖
- Docker Compose 配置
- Redis Docker 镜像依赖

---

## [v3.0] - 2026-08-01

### Added
- 升级到 .NET 10（从 .NET 8）
- ORM 支持 EF Core 10 / SqlSugar / Dapper 三框架可切换
- `IDbConnectionSwitcher` 接口支持按名称切换数据库连接
- 自封装 `messaging-service`（不依赖 RabbitMQ）
- 自封装 `logging-service`（不依赖 Seq/ELK）
- `BuildingBlocks.Communication`：HTTP / gRPC 可切换
- BI 分析管理平台（Blazor + MudBlazor + ECharts，参考 .NETAdmin）
- 环境检查结果（.NET 10 / Node 24 / SQL Server 已就绪）

### Changed
- 从 C#/.NET 8 升级到 .NET 10
- 消息队列从 MassTransit+RabbitMQ 改为自封装 SQL Server 持久化
- 日志从 Seq 改为自封装 logging-service
- 服务间通信从固定 gRPC 改为 HTTP/gRPC 可配置切换

---

## [v2.0] - 2026-08-01

### Changed
- 技术栈从 Java 21 + Spring Cloud Alibaba 改为 C# 12 / .NET 8 + ASP.NET Core
- API 网关从 Spring Cloud Gateway 改为 YARP
- 消息队列从 RocketMQ 改为 MassTransit + RabbitMQ
- ORM 从 MyBatis-Plus 改为 EF Core 8
- 数据库从 MySQL 改为 SQL Server
- 后台前端从 React Admin 改为 Blazor + MudBlazor
- 业务模型从单一电商改为多商户入驻平台
- 微服务数量从 10 个增加到 16 个
- 落地路线从 12 周调整为 14 周

---

## [v1.0] - 2026-08-01

### Added
- 初始方案：Java 21 + Spring Boot 3 + Spring Cloud Alibaba
- 普通电商平台，10 个微服务
- 12 周落地路线图

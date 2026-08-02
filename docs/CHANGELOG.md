# 变更记录

## [v4.9] - 2026-08-02

### Added
- **Phase 1 Week 5-6：开发完成 product-service（商品管理微服务，端口 8003）**：
  - 分类（商户自建，父子层级 + 排序 + 软停用）+ 商品 CRUD（含多 SKU：编码/规格/价格/库存）
  - 上下架状态机（Draft→OnSale→OffSale，上架需启用 SKU）
  - **多租户隔离落地**（首个业务级）：X-Merchant-Id 请求头 → ITenantProvider → 实体 MultiTenantEntity + DbContext HasQueryFilter + Handler 显式过滤三重防护
  - 数据库：MMP_Product 库（Categories / Products / ProductSkus）
  - 网关路由：`/api/product/**` → 8003
  - 延续规范：Mediator + CQRS + 充血实体 + 全 API 注解 + Swagger Bearer
- 新增模块文档 `docs/modules/product-service.md`

### Verified
- 全量编译 0 警告 0 错误（Release，17 个项目）
- 冒烟测试全通过：分类父子 → 商品(2SKU) → 上架 → 缺商户头400 → 跨商户隔离(空/404) → 删除保护 → Swagger

### Notes
- **多租户安全修复**：首版 Update/Delete/查询未强制商户上下文（HasQueryFilter 空商户时不拦截），已改为 Handler 显式 `Where(MerchantId)` + 商户上下文必检
- 踩坑：MultiTenantEntity.MerchantId 为 required，子类构造函数需 `[SetsRequiredMembers]`

---

## [v4.8] - 2026-08-02

### Added
- **Phase 1 Week 4-5：开发完成 merchant-service（商户入驻审核微服务，端口 8002）**：
  - 入驻申请（商户名唯一 + 一个用户仅一条未终态申请）+ 我的商户 / 详情 / 分页列表
  - 审核流程（admin 角色）：批准 / 驳回（驳回必填原因），状态机 Pending→Approved/Rejected→Disabled
  - 数据库：MMP_Merchant 库 Merchants 表（Name 唯一索引 + 多组合索引）
  - 网关路由：`/api/merchant/**` → 8002（YARP 预置路由生效）
  - 延续 identity-service 规范：Mediator + CQRS + 充血实体 + 全 API 注解 + Swagger Bearer
- **BuildingBlocks.Security 增强**：
  - `CurrentUserAccessor` 提升为公共实现（+AddCurrentUser 扩展），identity/merchant 共用（消除重复代码）
  - **修复 JWT role claim**：签发改用标准短名 `role`（原 ClaimTypes 长 URI 导致 [Authorize(Roles)] 失效），配合 MapInboundClaims=false + RoleClaimType="role"
  - 改用 FrameworkReference Microsoft.AspNetCore.App（Http.Abstractions 已并入共享框架）
- identity-service：移除本地 CurrentUserAccessor（改用公共实现），JwtBearer 补 RoleClaimType
- 新增模块文档 `docs/modules/merchant-service.md`

### Verified
- 全量编译 0 警告 0 错误（Release，16 个项目）
- 冒烟测试 12 项全通过：申请 → 我的商户 → 重复申请409 → 名称占用409 → 非admin 403 → admin 列表/详情 → 审核通过(Approved) → 重复审核400 → 驳回缺原因400 → 驳回(Rejected+原因) → Swagger

### Notes
- 踩坑记录：JwtTokenService 用 ClaimTypes.Role 长 URI 签发导致角色授权失效，改短名 role 解决；Http.Abstractions 3.0+ 并入共享框架需用 FrameworkReference

---

## [v4.7] - 2026-08-02

### Added
- **Phase 1 Week 4 启动：开发完成 identity-service（用户认证微服务，端口 8001）**：
  - 注册（邮箱唯一，注册即登录）+ 登录（密码校验）+ JWT 签发（复用 BuildingBlocks.Security）
  - 登录失败锁定：连续 5 次错误锁定 15 分钟（AuthOptions 可配置）
  - 密码安全：PBKDF2-SHA256（100k 迭代 + 随机盐 + 恒定时间比较），无第三方依赖
  - 数据库：MMP_Identity 库 Users 表（Email 唯一索引）
  - 网关路由：`/api/identity/**` → 8001（YARP 预置路由生效）
  - **首个按编码规范 Phase 1 分层落地的服务**：Mediator + CQRS（Command/Query + Handler 自动扫描注册）、User 充血实体（状态机内聚）、全部 API XML 注解、Swagger 带 Bearer 认证按钮
- **BuildingBlocks.Core 新基建**：
  - `Mediator` 实现（IMediator 默认实现，DI 路由 Handler）
  - `AddCqrsHandlers` 程序集扫描注册 CQRS 处理器
  - 引用 Microsoft.Extensions.DependencyInjection.Abstractions
- **BuildingBlocks.Security 修复**：JwtOptions.SecretKey 去掉 required（required 带默认值属反模式）
- 新增模块文档 `docs/modules/identity-service.md`

### Verified
- 全量编译 0 警告 0 错误（Release，15 个项目）
- 冒烟测试全通过：注册 → 重复注册409 → 登录 → JWT 查 me → 无 token 401 → 5 次失败锁定 → 锁定后拒登 → 网关转发

### Notes
- 踩坑记录：JWT sub/role claim 默认被 inbound 映射改名，须 `MapInboundClaims=false` 保留原始 claim；Microsoft.OpenApi 2.0 中 SecurityRequirement 用 `OpenApiSecuritySchemeReference`

---

## [v4.6] - 2026-08-02

### Added
- **三个服务 API 全量补 XML 注解**（messaging / logging / email，约 100+ 处）：
  - Controller 类 + 全部 Action 的 summary/param/returns（10 个 Controller）
  - DTO 请求/响应类全部属性注释（3 个 DTO 文件）
  - 实体构造函数 / 领域方法 / DbContext DbSet / DI 注册扩展 注释补全
- 三个服务 csproj 开启 `GenerateDocumentationFile=true` + 取消 CS1591 屏蔽（缺注释编译即警告）
- Program.cs SwaggerGen 配置 `IncludeXmlComments`，Swagger UI 展示全部注解
- **编码规范新增第八节「API 注解规范」**（docs/architecture/coding-standards.md）：
  - API 项目必须开 XML 生成且不得屏蔽 CS1591；注释覆盖范围；SwaggerGen 加载配置；0 警告 0 错误验收口径
  - Phase 1 验收标准追加「API 注解」条款；禁止事项追加「API 无注解即不合格」

### Verified
- 三个服务 Release 编译全部 0 警告 0 错误
- Swagger UI 注解展示正常（Controller/Action/参数/DTO 属性均有描述）

### Notes
- 踩坑记录：Swashbuckle 7.0.0 与 .NET 10 不兼容（TypeLoadException），升级至 10.1.7（v4.5 已记）

---

## [v4.5] - 2026-08-02

### Added
- 新增编码规范文档 `docs/architecture/coding-standards.md`（v1.0，全项目强制）：
  - 三大特性规范：封装（private set + 领域方法 + 充血模型，EmailMessage 为样板）/ 继承（Entity/MultiTenantEntity 体系）/ 多态（6 处扩展点接口）
  - 设计模式应用清单（Aggregate Root / Specification / Mediator+CQRS / UnitOfWork / Strategy / Observer / Factory / Template Method）
  - 开闭原则、高内聚低耦合、消息订阅（Pub/Sub）落地要求
  - **Phase 1 业务服务开发规范（10 条强制）**：Mediator 分层（Controller→IMediator→Handler→领域服务→Repository）、CQRS 分离、订阅收敛到网关、多租户隔离、UnitOfWork 事务、消息幂等、邮件接入方式、每服务验收标准
  - 禁止事项红线（Controller 直连仓储 / 裸 setter / 改旧代码扩展 / 散落 SaveChanges / DateTime.Now 等）

### Notes
- 文档分类约定落地：架构/规范类归 `docs/architecture/`，索引与 DOC_INDEX.md 已同步

---

## [v4.4] - 2026-08-02

### Added
- 开发完成 **email-service**（自封装邮件微服务，Phase 0 Week 3）：
  - 邮件发送（MailKit SMTP）+ DryRun 开发模式（本地无 SMTP 可模拟）
  - Razor 模板渲染（RazorLight，@Model.xxx 变量插值）+ 模板 CRUD
  - 发送状态机：Pending/Sent/Failed/DeadLetter + 后台指数退避重试（60s ×2 上限 10min）
  - API：发送/批量/状态查询/手动重试/死信 + 模板管理 + 健康检查
  - 数据库：MMP_Email 库（EmailMessage / EmailTemplate）
  - 网关路由：`/api/emails/**`、`/api/templates/**` → 8015
- **BuildingBlocks.Data ORM 切换完善**（Strategy + Factory 模式）：
  - `SqlSugarRepository<T>`（SqlSugar 实现，InsertableByObject 兼容 EF 风格实体）
  - `DapperRepository<T>`（Dapper 实现，反射生成 SQL + 存储过程扩展点）
  - `IOrmStrategy` 策略标记 + `IRepositoryFactory` 仓储工厂（按 DataOptions.DefaultOrm 自动解析）
  - `AddDataLayer` 完整注册三种 ORM 实现与 SqlSugarScope 单例
- **BuildingBlocks.Communication gRPC 完善**：
  - `GrpcServiceClient`：JSON-gRPC 模式通用客户端（path 格式 "ServiceName/MethodName"）
  - `AddServiceClient` 支持 `CommunicationProtocol.Grpc` 注册
  - 新增 Grpc.Net.Client / Grpc.Core.Api 2.80.0（匹配 Aspire 13.4.6 依赖链）
- 新增模块文档 `docs/modules/email-service.md`

### Verified
- 全量编译 0 警告 0 错误（Release，14 个项目）
- email-service 冒烟测试通过：健康检查 → 创建模板 → 模板渲染发送（Welcome Xiaoma!）→ 直接发送 → 分页查询

---

## [v4.3] - 2026-08-02

### Added
- 开发完成 **logging-service**（自封装日志管理微服务，Phase 0 Week 2）：
  - 批量接收日志（POST /api/logs/batch）+ SQL Server 持久化（MMP_Infra · Logs 表）
  - 检索：按服务/级别/关键字/时间范围分页查询
  - 统计：级别分布 / Top 服务 / 时间趋势（小时/天）/ 错误率
  - 索引设计：Timestamp / (Service,Timestamp) / (Level,Timestamp) / TraceId
  - 网关路由：`/api/logs/**`、`/api/log-stats/**` → 8011
- 完善 **BuildingBlocks.Logging** 客户端：
  - 缓冲上限保护（10,000 条，防内存溢出）
  - 上报请求 10 秒超时
- 新增模块文档 `docs/modules/logging-service.md`

### Verified
- 全量编译 0 警告 0 错误（Release）
- 冒烟测试通过：健康检查 → 批量上报 5 条 → 查询/过滤 → 级别分布 → Top 服务 → 错误率 40% → 趋势

### Notes
- 本机存在 2 个提权遗留进程（MessagingService.exe PID 4202324 及其 dotnet 宿主 PID 1496，冒烟测试遗留）锁定 Debug 输出目录，需管理员权限结束或重启电脑后恢复 Debug 构建；Release 构建不受影响

---

## [v4.2] - 2026-08-02

### Added
- 开发完成 **messaging-service**（自封装消息队列微服务，Phase 0 Week 2）：
  - Outbox 模式：消息先落库（SQL Server · MMP_Infra）再异步投递，不丢消息
  - 发布订阅：REST 发布 + 后台分发器（BackgroundService 轮询）+ HTTP 回调订阅者
  - 指数退避重试（5s ×2，上限 5 分钟）+ 超限自动转死信 + 手动重试
  - 幂等去重：Idempotency 表记录 (MessageId, ConsumerUrl)，防止重复投递
  - REST API：发布/批量/状态查询/手动重试/死信管理 + 订阅管理 + 健康检查
  - 网关路由：`/api/messages/**`、`/api/subscriptions/**`、`/api/health/**` → 8010
- 扩展 **BuildingBlocks.Messaging**：
  - `HttpMessagePublisher`：通过 HTTP 调 messaging-service 发布（Strategy — HTTP 策略）
  - `MessageConsumer<T>`：订阅者消费者基类（反序列化 + 业务处理 + 消费结果）
  - `MessageBusOptions` + `AddHttpMessageBus()/AddInMemoryMessageBus()` DI 注册
- 新增模块文档 `docs/modules/messaging-service.md`（API / 订阅者接入 / 配置 / 验证结果）

### Changed
- `Directory.Packages.props`：Microsoft.Data.SqlClient 5.2.2 → 6.1.1（EF Core 10 依赖要求）
- AspireHost 注册 messaging-service（端口 8010）
- 解决方案新增第 12 个项目 MessagingService

### Verified
- 全量编译 0 警告 0 错误
- 冒烟测试通过：健康检查 → 发布 → 订阅 → 后台投递回调 200 → 消息终态 Published

---

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

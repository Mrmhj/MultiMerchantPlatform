# 编码规范（Coding Standards）

> **文档路径**：`E:\MultiMerchantPlatform\docs\architecture\coding-standards.md`
> **所属阶段**：全项目强制 · **版本**：v1.0
> **更新日期**：2026-08-02

---

## 一、总体原则

| 原则 | 要求 |
|------|------|
| **先设计后实现** | 任何功能先方案、后架构、再编码；方案变更同步更新文档 |
| **架构先行** | Phase 0 搭底座（BuildingBlocks/网关/基础设施），Phase 1+ 才做业务功能 |
| **面向接口编程** | 依赖抽象，不依赖实现；跨层调用必须走接口 |
| **开闭原则（OCP）** | 新增能力 = 新增类 + DI 注册；**禁止修改既有实现**来扩展 |
| **高内聚低耦合** | 服务单一职责；依赖通过接口注入；服务间用事件解耦，不互相 new |
| **消息订阅** | 服务间异步通知走 `IMessagePublisher`（Pub/Sub），同步调用走 `IServiceClient` |

---

## 二、C# 13 / .NET 10 语法规范

- 主构造函数（primary constructor）优先
- `record` 用于 DTO / 结果 / 值对象；`class` 用于实体 / 服务
- `required` 修饰必填成员；集合表达式 `[]` 替代 `new List<T>()`
- `ArgumentException.ThrowIfNull/ThrowIfNullOrWhiteSpace` 校验参数
- 一律 `file-scoped namespace`
- 时间依赖使用 `TimeProvider`（可测试），禁止直接 `DateTime.Now`
- 实体基类属性用 `protected set`，业务实体用 `private set`

---

## 三、封装规范（实体必须遵循）

**样板：`EmailMessage`（email-service/Domain/Entities）**

1. **属性全部 `private set`**——禁止裸 setter，外部无法篡改状态
2. **状态变更走领域方法**：`MarkSent()` / `MarkFailed()` / `ResetForRetry()` 范式，
   非法状态转移（如跳过 Pending 直接置 Sent）在编译期杜绝
3. **私有构造函数**（仅供 EF Core）+ 公开工厂构造函数（带参数校验）
4. **充血领域模型**：业务行为（状态机、退避计算、规则判断）内聚在实体内部，
   禁止贫血模型（实体只当数据载体）
5. 领域事件通过 `protected AddDomainEvent()` 内聚添加，外部只能读

---

## 四、继承规范

- 实体必须继承 `Entity`（统一 Id / CreatedAt / 领域事件 / 值相等）
- 商户相关实体继承 `MultiTenantEntity`（`required Guid MerchantId` 强制多租户隔离）
- 继承用于**复用共性**（审计字段/领域事件/多租户），禁止为凑复用而继承
- 继承层级 > 3 层需架构评审
- 命令/查询处理器继承 `ICommandHandler<TCommand,TResult>` / `IQueryHandler<TQuery,TResult>` 泛型体系

---

## 五、多态规范（扩展点清单）

以下扩展点**必须**通过接口 + DI 注册实现多态，新增实现禁止改动现有类：

| 扩展点 | 接口 | 已实现 |
|--------|------|--------|
| 数据访问 | `IRepository<T>` | EF / Dapper / SqlSugar（Strategy） |
| 服务通信 | `IServiceClient` | HTTP（默认）/ gRPC |
| 消息传输 | `IMessagePublisher` | InMemory（开发）/ HTTP（生产） |
| 邮件渠道 | `ISmtpSender` | SmtpSender(MailKit) / DryRun |
| 缓存 | `ICacheService` | InMemory / Memurai（后续） |
| 租户 | `ITenantProvider` | TenantContext |

**规则**：新增实现类 + `AddXxx()` 一行注册；运行时按配置/环境切换；禁止用 `if-else` 分支扩展类型。

---

## 六、设计模式应用清单

| 模式 | 代码位置 | 使用要求 |
|------|----------|---------|
| Aggregate Root | `BuildingBlocks.Core/Entities/Entity.cs` | 聚合根实现 `IAggregateRoot`，业务实体必须 |
| Observer | `Entity.DomainEvents` + `IDomainEventDispatcher` | 状态变更发布领域事件 |
| Specification | `BuildingBlocks.Core/Specifications/ISpecification.cs` | 复杂查询条件对象化，支持 And/Or/Not |
| Mediator + CQRS | `BuildingBlocks.Core/CQRS/Interfaces.cs` | **Phase 1 起强制**：业务入口走 `IMediator` |
| Unit of Work | `BuildingBlocks.Data/Implementations/EfUnitOfWork.cs` | 事务边界统一提交，禁止散落 SaveChanges |
| Repository | `BuildingBlocks.Data/Abstractions/IRepository.cs` | 数据访问唯一入口 |
| Strategy | ORM/通信/消息/邮件/缓存五处 | 见"五、多态规范" |
| Factory | `RepositoryFactory` / `DbConnectionSwitcher` | 按配置解析实现 |
| Template Method | `BuildingBlocks.Messaging/MessageConsumer<T>` | 消息消费者继承基类，只实现 `HandleAsync` |

---

## 七、Phase 1 业务服务开发规范（强制）

> 适用范围：identity / merchant / product / order / pay / stock / cart / search / promotion / review / logistics / settlement

1. **实体范式**：严格遵循第三节封装规范（private set + 领域方法 + 充血模型），
   所有业务实体以 `EmailMessage` 为样板
2. **业务分层（Mediator 强制）**：
   ```
   Controller → IMediator → ICommandHandler/IQueryHandler → 领域服务 → IRepository
   ```
   禁止 Controller 直接依赖 Application 服务或仓储
3. **CQRS 分离**：写操作走 Command，读操作走 Query，禁止混用
4. **订阅收敛**：事件订阅回调端点统一走 YARP 网关入口，禁止在 messaging-service
   配置硬编码服务直连地址
5. **多租户隔离**：商户相关实体必须 `MultiTenantEntity`；查询强制带 `MerchantId` 过滤
6. **领域事件**：状态变更必须经实体方法触发，由 `IDomainEventDispatcher` 分发
7. **事务边界**：跨仓储写操作包在 `IUnitOfWork` 内统一提交
8. **消息幂等**：消费消息用 `MessageId`（X-Message-Id）作幂等键 + 数据库唯一约束
9. **邮件接入**：验证码/通知类邮件通过 `IServiceClient` 调 email-service `/api/emails`，
   异步场景发 `IMessagePublisher` 事件由订阅方投递
10. **API 注解**：所有 Controller / Action / DTO 必须含 XML 注释（见第八节），
    缺注释导致 CS1591 警告 = 验收不通过
11. **验收标准**：每个服务交付 = 可编译(0警告0错误) + 冒烟测试通过 + 模块文档 +
    CHANGELOG 条目 + 网关路由接入

---

## 八、API 注解规范（强制，2026-08-02 起执行）

> 背景：曾发生 API 全部无注解、Swagger 空白的问题。以下为强制要求，缺注释编译即警告。

1. **XML 文档生成**：Web API 项目 csproj 必须开启：
   ```xml
   <GenerateDocumentationFile>true</GenerateDocumentationFile>
   <NoWarn></NoWarn>   <!-- 不得屏蔽 CS1591，让缺注释以警告暴露 -->
   ```
   （Directory.Build.props 全局默认关闭，服务项目需自行开启；BuildingBlocks 库项目不受限）
2. **注释覆盖范围**（缺一即 CS1591 警告）：
   - Controller 类：类职责说明
   - 每个 Action：`<summary>` 功能说明 + `<param>` 每个参数 + `<returns>` 返回值
   - DTO 类：用途说明；每个属性：字段含义
   - 实体构造函数 / 公共方法 / DbContext DbSet 属性
3. **Swagger 读取**：Program.cs 中 SwaggerGen 必须加载 XML：
   ```csharp
   builder.Services.AddSwaggerGen(options =>
   {
       var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
       var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
       options.IncludeXmlComments(xmlPath);
   });
   ```
4. **Swagger UI 环境限制**：`UseSwagger/UseSwaggerUI` 仅 Development 环境启用，
   生产环境关闭（防接口信息泄露）；正式文档以 `docs/modules/` 模块文档为准
5. **验收口径**：编译输出必须 0 警告 0 错误（含 CS1591）；提交前自检 `dotnet build`

- 文档按类别分目录输出（2026-08-02 约定）：
  - `docs/` 根：PROJECT_PLAN / CHANGELOG / DOC_INDEX（总纲不动）
  - `docs/modules/`：模块文档（各服务 API/配置/验证）
  - `docs/architecture/`：架构设计、编码规范
  - `docs/reports/`：进度 / 测试 / 验收报告
  - `docs/database/`：库表设计、迁移说明
  - `docs/guides/`：开发 / 部署 / 运维指南
- 新增文档必须登记 `docs/DOC_INDEX.md`，版本变更追加 `docs/CHANGELOG.md`
- 每次架构调整同步更新所有关联文档，禁止文档滞后于代码

### 阶段交付流程（强制，2026-08-02 起执行）

```
阶段任务完成 → 验证（编译 0 警告 0 错误 + 冒烟测试通过）
    → 提交 Git（commit + push origin）→ 再进入下一阶段任务
```

- 每个阶段（如每个微服务、每个功能批次）完成且验证无误后，**必须优先提交 Git**，不允许带未提交变更进入下一阶段
- 提交信息遵循约定：`feat: <阶段说明> (<版本号>)`，正文列出关键变更点
- 提交前确认工作区只含本阶段相关变更（`git status` 检查）

### 会话边界约定（强制，2026-08-02 起执行）

```
每完成一个服务（编译 0 警告 0 错误 + 冒烟测试通过 + 文档交付 + 提交 Git）
    → 开新会话继续下一步
```

- **阶段边界 = 会话边界**：单会话连续运行会导致上下文过长、token 消耗平方级增长（历史实测单会话 ~3.2 亿 token，88% 为上下文重发）；按服务开新会话可省 50-84%
- **新会话恢复方式（强制）**：第一步**整读 `docs/CONTEXT.md`**（项目上下文文件，含进度/服务清单/下一步/约定/踩坑，维护于 docs 根目录）——一次整读即可恢复，必要时再 `git log --oneline -5` 校验提交状态
- 每完成一个服务交付后，主动提示小马哥开新会话再继续
- 长任务（>2 个服务 / >100 轮）必须拆分会话，禁止单会话跨多服务
- **上下文维护**：每完成一个阶段，必须同步更新 `docs/CONTEXT.md`（当前进度/下一步），保证新会话整读即最新

---

## 十、禁止事项（红线）

- ❌ 禁止 Controller 直连仓储 / DbContext / 第三方客户端
- ❌ 禁止实体公开 setter 无行为校验
- ❌ 禁止为扩展修改既有实现类（应新增实现 + DI 注册）
- ❌ 禁止服务间直接 new 对方客户端（必须经网关 + IServiceClient / 消息总线）
- ❌ 禁止散落 `SaveChangesAsync`（必须 UnitOfWork 统一）
- ❌ 禁止 `DateTime.Now` 直用（必须 TimeProvider）
- ❌ 禁止 API 无 XML 注解或屏蔽 CS1591（Swagger 空白即不合格）
- ❌ **禁止任何涉密信息提交 Git**（见第十一节，违规提交即红线）

---

## 十一、涉密信息管理规范（强制，2026-08-02 起执行）

### 涉密信息范围

| 类别 | 示例 | 存放位置 |
|---|---|---|
| 数据库连接串 | `Server=...;User Id=sa;Password=...` | 本地 `appsettings.json` |
| JWT 签名密钥 | `Jwt:SecretKey` | 本地 `appsettings.json` |
| 服务间调用密钥 | `Internal:Key`（X-Internal-Key） | 本地 `appsettings.json` |
| 第三方凭证 | SMTP 密码、短信/Push 网关密钥、支付密钥 | 本地 `appsettings.json` / 环境变量 |
| 证书私钥 | `*.pfx` `*.p12` `*.key` `*.pem`（私钥） | 本机证书库 / 安全目录 |
| 环境变量凭据 | 前端 `.env*` 中的真实地址+凭据 | 本地 `.env.local` |

### 强制规则

1. **本地实际配置一律不入库**：各服务 `appsettings.json` / `appsettings.*.json`（Development/Production 等）全部 Git 忽略，仅提交 `appsettings.Example.json` 模板（敏感值用占位符 `__XXX__`：`__DB_PASSWORD__` / `__JWT_SECRET__` / `__INTERNAL_KEY__` / `__PASSWORD__`）
2. **新服务必建模板**：新增服务创建 `appsettings.json` 后必须同步生成 `appsettings.Example.json`（结构一致、敏感值占位符），否则不予验收
3. **前端 env 模板化**：`.env.production` 等实际配置不入库，提供 `.env.production.example` 模板
4. **禁止硬编码密钥兜底**：配置读取禁止 `GetValue("...", "真实密钥")` 兜底；缺配置应启动失败而不是静默降级
5. **密钥轮换**：一旦发现密钥已进入 Git 历史（含远端仓库），必须立即轮换新值（所有服务同步），并在 CHANGELOG 记录
6. **Git 提交前检查**：`git status` 确认无 `appsettings.json`/`.env*`（非 example）后提交；新增敏感文件类型先更新 `.gitignore`
7. **历史清理**：存量敏感值已在远端历史的，评估风险后决定是否 `git filter-repo` 改写历史（需 force push，仅限仓库唯一使用人，先备份）

### 占位符约定

| 占位符 | 含义 |
|---|---|
| `__DB_PASSWORD__` | 数据库连接串密码 |
| `__JWT_SECRET__` | JWT 签名密钥 |
| `__INTERNAL_KEY__` | X-Internal-Key 服务间调用密钥 |
| `__PASSWORD__` | 通用密码字段（SMTP/第三方） |


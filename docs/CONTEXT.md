# 项目上下文（会话恢复专用）

> **用途**：新会话开始时**整读本文件**即可恢复项目上下文（不必翻历史对话）。
> **维护**：每个阶段（服务）交付后必须同步更新本文件的「当前进度」与「下一步」。
> 版本对应：v7.3 · 2026-08-02 · Phase 4 Week 18 缓存优化+分库分表评估完成

---

## 一、项目概览

**多商户电商平台（MultiMerchantPlatform）** — 微服务架构，.NET 10 / C# 13 / SQL Server 2025 / Vue 3 前端。22 周路线图（Phase 0-5），当前 **Phase 2**。

- 服务间通信：YARP 网关（8000）+ `IServiceClient`（HTTP，可切 gRPC）+ 消息总线（messaging-service）
- 鉴权：JWT（identity-service 签发，`MapInboundClaims=false`，role 用短名）
- 多租户：商户维度 `X-Merchant-Id` 头 + `MultiTenantEntity` + `HasQueryFilter` + Handler 显式过滤
- 服务间内部调用：`X-Internal-Key`（MMP-Internal-Key-2026）

## 二、服务清单（19 服务 + 网关）

| 服务 | 端口 | 数据库 | 状态 | 说明 |
|---|---|---|---|---|
| identity-service | 8001 | MMP_Identity | ✅ | 注册/登录/JWT/失败锁定 |
| merchant-service | 8002 | MMP_Merchant | ✅ | 入驻/审核/店铺 + 内部查商户名 |
| product-service | 8003 | MMP_Product | ✅ v7.3 | 分类/商品/SKU/上下架 + C 端公开接口（**Redis 热数据缓存**） |
| order-service | 8004 | MMP_Order | ✅ | 跨商户拆单/状态机/库存联动 |
| pay-service | 8005 | MMP_Pay | ✅ | 支付单/模拟支付/退款/回调订单 |
| stock-service | 8006 | MMP_Stock | ✅ | 库存预占/扣减/释放 + 内部接口 |
| **cart-service** | 8007 | MMP_Cart | ✅ v5.5 | 购物车（买家隔离/同 SKU 合并） |
| **search-service** | 8008 | MMP_Search | ✅ v5.5 | 商品搜索索引（在售/关键词/价格） |
| **promotion-service** | 8009 | MMP_Promotion | ✅ v7.3 | 优惠券/满减活动/内部核销 + **秒杀**（Redis 预扣+异步下单）+ C 端活动列表缓存 |
| **review-service** | 8012 | MMP_Review | ✅ v5.8 | 商品评价（买家/商户/公开） |
| **logistics-service** | 8013 | MMP_Logistics | ✅ v5.9 | 物流（运单/轨迹/公司，订单发货联动） |
| **settlement-service** | 8014 | MMP_Settlement | ✅ v5.9 | 结算（佣金规则/结算单/幂等生成） |
| **im-service** | 8016 | MMP_IM | ✅ v6.0 | 即时通讯（SignalR：私聊/客服群/已读/离线补推/内部推送） |
| **performance-service** | 8017 | MMP_Infra | ✅ v6.3 | 压测（HTTP 并发/HTML 报告）+ 监控（内存/CPU/GC/线程池）+ 告警 |
| **risk-service** | 8018 | MMP_Risk | ✅ v6.4 | 风控/反刷单（规则引擎/事件上报/决策/黑名单/案例处置） |
| **notification-service** | 8019 | MMP_Notification | ✅ v6.6 | 通知中心（站内信/短信/Push/模板/公告广播/SignalR 实时推送） |
| **bi-admin-service** | 8020 | MMP_BI | ✅ v7.0 | BI 分析（跨服务聚合统计/按天销售/商户商品排行/状态分布/总览快照） |
| messaging-service | 8010 | MMP_Infra | ✅ | 消息总线（Outbox/通配订阅） |
| logging-service | 8011 | MMP_Infra | ✅ | 日志批量上报/查询/统计 |
| email-service | 8015 | MMP_Email | ✅ | 邮件（MailKit/DryRun/模板/重试） |
| ApiGateway（YARP） | 8000 | — | ✅ v7.3 | 路由转发 + **入口限流（RateLimiter：并发/固定窗口/令牌桶）** |

前端：`src/apps/web-customer`（Vue 3.5 + Vite 8 + Element Plus，C 端商城，端口 5173 dev）
前端：`src/apps/web-merchant`（Vue 3.5 + Vite 8 + Element Plus，商户端，端口 5174 dev）
前端：`src/apps/mobile-app`（uni-app Vue 3，C 端移动商城，H5 端口 5175 dev）
前端：`src/apps/desktop-app`（Electron 33 + Vue 3.5，商户工作台，渲染端口 5176 dev；公告中心/内部邮件/通知收件箱）
前端：`src/apps/web-admin`（Vue 3.5 + Vite 8 + Element Plus + ECharts，平台管理后台 BI 看板，端口 5177 dev）

## 三、当前进度

- **Phase 4 Week 18 已完成（v7.3）**：缓存策略优化 + 数据库分库分表评估 + 限流熔断 —— ① 网关 RateLimiter 三层链式限流（并发→按 IP 固定窗口→秒杀令牌桶，429 实测 5×200+5×429）② IServiceClient 内置 Polly v8 弹性（重试/熔断/超时，IOptions 配置节化，调用方零改动）③ ICacheService 新增 GetOrAddAsync 防击穿（Redis SETNX 锁/InMemory 信号量双实现）④ product 商品详情/列表 Redis 热数据缓存 + 版本失效、promotion 秒杀活动列表缓存 ⑤ 分库分表评估报告（方案 A 表分区首选）+ docs/database/ 目录落地；冒烟 tests/smoke-week18.sh **14/14 通过**
- **Phase 4 Week 17 已完成**：秒杀场景实现（缓存预扣 + 异步下单）—— BuildingBlocks.Cache 接 Redis（StackExchange.Redis + 分布式锁 + Lua 原子预扣防超卖）、promotion-service 秒杀模块（SeckillActivity/SeckillRecord + 抢购 + 超时回滚后台任务）、order-service 异步秒杀下单（幂等表 + 消息消费端点）、消息发布器/客户端双修复；冒烟 tests/smoke-seckill.sh **13/13 通过**
- **Phase 3 Week 16 已完成**：BI 分析管理平台（提交见 git log）—— bi-admin-service 8020 + web-admin 前端（Vue 3 + ECharts）✅ **Phase 3 全部完成**
- **Phase 3 Week 15-16 已完成**：desktop-app 桌面端 Electron（提交见 git log）—— 商户工作台（公告中心/内部邮件/通知收件箱，短信/Push 真实网关暂缓仅内部公告+内部邮件）
- **Phase 3 Week 15 已完成**：notification-service 通知中心 v6.6（提交见 git log）—— 新增公告广播模块（Announcement + AnnouncementRead）
- **Phase 3 Week 14 已完成**：risk-service 风控/反刷单（提交见 git log）—— 风控规则引擎 ✅
- **Phase 3 Week 14 已完成**：performance-service 压测+内存监控（提交见 git log）
- **Phase 2 Week 13 已完成**：mobile-app 移动端骨架（提交见 git log）—— **Phase 2 全部完成**
- **Phase 2 Week 12-13 已完成**：web-merchant 商户端 Web 前端（提交见 git log）
- **Phase 2 Week 12 已完成**：im-service（提交见 git log）
- **Phase 2 Week 11 已完成**：logistics-service + settlement-service（提交 df07235）
- **Phase 2 Week 10-11 已完成**：review-service（提交 d90447d）
- **Phase 2 Week 10-11 已完成**：promotion-service（提交 a83aa99）
- **Phase 2 Week 10 已完成**：cart-service + search-service（提交 c5512d7）
- **Phase 1 全部完成**（v4.7-v5.4）：identity → merchant → product → order → pay → stock → 库存联动 → C 端 Web 商城
- 全量编译 **0 警告 0 错误**（31 项目）；最近提交见 git log

## 四、下一步（按 PROJECT_PLAN.md 路线图）

> **Phase 4（第 17-19 周）进行中**：Week 17 秒杀 ✅（v7.2）→ Week 18 缓存+分库分表 ✅（v7.3）→ **Week 19：performance-service 全量压测 + 瓶颈优化**

| 周次 | 任务 | 端口/说明 |
|---|---|---|
| 17 | ~~秒杀场景实现（缓存预扣 + 异步下单）~~ | ✅ v7.2（Redis + 分布式锁 + 异步下单 + 超时回滚） |
| 18 | ~~缓存策略优化 + 数据库分库分表~~ | ✅ v7.3（Redis 热数据缓存 + 限流熔断 + 分库分表评估报告） |
| 19 | **performance-service 全量压测 + 瓶颈优化** | 压测报告（reports/） |
| 20-22 | Phase 5：部署上线（Windows Service / K8s、监控告警、灰度上线） | 见 PROJECT_PLAN |

> **Phase 4 前置（2026-08-02 已落地）**：
> - **Redis 已部署**：tporadowski 5.0.14.1 Windows 版 `E:\redis-5.0.14\`，Windows 服务 `redis`（自启），0.0.0.0+[::]:6379（防火墙已放行），密码 `MMP-Redis-PUctKhVRIFB48kmfI6Ek`；局域网 192.168.1.4 / 公网 IPv6 2409:8a62:...（动态）/ **公网 IPv4 36.170.45.77 为移动 CGNAT 不可直连（需内网穿透）**；详见 `docs/guides/redis-setup.md`
> - **涉密信息规范（红线）**：本地 `appsettings.json`/`.env*` 一律不入库，仅提交 `appsettings.Example.json` 模板（占位符 `__DB_PASSWORD__`/`__JWT_SECRET__`/`__INTERNAL_KEY__`）；JWT SecretKey 与 Internal Key 已轮换新值（旧值曾入 GitHub 历史）；见 coding-standards.md 第十一节
> - BuildingBlocks.Cache 已入库 Redis 实现（ICacheService/RedisDistributedLock + StackExchange.Redis 2.8.16），连接串格式 `host:port,password=xxx`；**v7.3 新增 GetOrAddAsync 防击穿 + 网关限流 + Polly 弹性**
> - **限流注意**：FixedWindow QueueLimit 生产配置 20（排队不悬挂）；设 0 则超限立即 429。秒杀 buy / 压测接口走令牌桶（2000/60s）

## 五、工作流程约定（强制）

1. **阶段交付**：阶段完成 → 编译 0 警告 0 错误 + 冒烟测试 → 提交 Git（commit+push）→ 再进下一阶段
2. **会话边界**：每完成一个服务（编译+冒烟+文档+提交）→ 开新会话继续；新会话第一步**整读本文件**恢复上下文
3. **文档分类**：`modules/`（模块）、`reports/`（报告）、`architecture/`（架构+规范）、`database/`（库表）、`guides/`（指南）；PROJECT_PLAN/CHANGELOG/DOC_INDEX/CONTEXT 留根目录
4. **API 注解**：服务开 GenerateDocumentationFile + IncludeXmlComments，0 警告交付
5. **分层**：Mediator + CQRS、充血实体（private set + 领域方法）、多租户三重防护、X-Internal-Key 内部调用

## 六、已踩坑清单（避免重犯）

- 多个命名 HttpClient 注册同一服务类型互相覆盖 → 客户端注入 `IHttpClientFactory` 按名 `CreateClient`
- 网关：Controller 自带 `api/xxx` 前缀的服务（cart/search）路由**不做前缀剥离**
- JWT role 用长 URI 名致角色授权失效 → 签发短名 "role" + `MapInboundClaims=false`
- Swashbuckle 需 10.1.7+；Microsoft.OpenApi 2.0 类型在 `Microsoft.OpenApi` 命名空间（非 Models）
- 服务 Release 直跑端口用 `ASPNETCORE_URLS` 显式指定
- **服务直跑必须 cd 到 bin/Release/net10.0 目录**：`dotnet xxx.dll` 从项目根启动读不到 appsettings.json（content root 为当前目录），报「缺少 Jwt 配置节」
- EF 关系修复致 `TryComplete` 误判 → 必须 Include 全部子单再判断
- **EF 子实体误判 UPDATE**：充血模型下新建子实体（客户端 Guid 主键）经导航集合添加被推断为 Unchanged → 0 行并发异常 → 必须显式 `db.Set<T>().Add()` 标记 Added
- 内部接口 `[FromHeader] string key`（非空引用类型）在 [ApiController] 下缺头自动 400（与错误密钥 401 语义一致，系统统一行为）
- 测试/演示后停服务进程（PowerShell `Get-NetTCPConnection -LocalPort` 批量停）；**运行中服务锁定 bin/Release/*.dll，重编译前先停**
- **EF 表达式树禁用自定义方法**：`IsExpired()`/维度匹配等静态方法不能用于查询谓词 → 内联可翻译表达式或按枚举分支写多个查询
- **EF 表达式树禁用 `is ... or ...` 模式匹配** → 改用 `== || ==` 等值比较
- **冒烟维度键必须每次运行唯一**（时间戳生成 GUID 末段）：固定 IP/用户会在窗口内残留历史事件污染统计
- **IMediator 无单泛型 SendAsync**：无返回值命令调用须显式 `SendAsync<TCommand, Unit>`（Unit 为空 record）
- **SignalR 推送语义**：`IHubContext.Clients.User(id)` 无在线连接时静默丢弃，REST 落库为最终一致，实时通道仅加速感知
- **PagedResult 序列化为 `totalCount/page/pageSize`**（camelCase）：冒烟断言勿用 `total`
- **Aspire AppHost 新增服务**：需同时更新 slnx + AppHost.csproj ProjectReference（否则 `Projects.X` 命名空间缺失报 CS0234）
- **GitHub release 下载**：直连极慢 → 查 Gitee 镜像仓库 release 附件；curl 报 23 write error = 后台残留 curl 占用同一目标文件 → Stop-Process 清理；PowerShell Invoke-WebRequest 更稳
- **tporadowski redis-server**：服务注册不支持 `--service-port`；Start-Process 起的进程随会话退出，长驻必须注册 Windows 服务
- **涉密治理**：本地配置模板化（appsettings.Example.json），提交前 `git diff --cached` 扫描敏感值
- **.NET 10 RateLimiter API 变化**：`PartitionedRateLimiter.Create<TResource,TPartitionKey>` 仅接收**返回 `RateLimitPartition` 的 partitioner 单参数**（旧版 partitionKey+factory 双参签名已移除）；分区器直接返回 `RateLimitPartition.GetFixedWindowLimiter/GetTokenBucketLimiter(...)`
- **限流队列悬挂**：FixedWindowRateLimiter QueueLimit>0 时超限请求排队等窗口刷新（最长一个窗口），短超时客户端表现为连接超时（000）而非 429 → 网关语义用 QueueLimit=0 立即拒绝
- **Redis 5.0.14 redis-cli 不支持 `--scan`**：查键用 `KEYS "pattern"`（开发环境可接受；生产禁 KEYS 用 SCAN 游标）
- **Polly v8 OnRetry 签名**：回调返回 `ValueTask`（`args.Outcome.Result?.Dispose()` 后必须 `return ValueTask.CompletedTask`）
- **服务主程序 DLL 命名**：`{ServiceName}.dll`（identity-service → IdentityService.dll，去掉 `-service` 后缀），启动脚本/文档注意

## 七、关键文档索引

| 文档 | 路径 |
|---|---|
| 路线图 | `docs/PROJECT_PLAN.md`（v4.1，22 周） |
| 编码规范 | `docs/architecture/coding-standards.md`（v1.0） |
| 变更记录 | `docs/CHANGELOG.md`（当前 v7.0） |
| 文档索引 | `docs/DOC_INDEX.md` |
| Redis 指南 | `docs/guides/redis-setup.md` |
| Token 分析 | `docs/reports/token-usage-analysis.md` |
| 模块文档 | `docs/modules/<service>.md` × 16 |

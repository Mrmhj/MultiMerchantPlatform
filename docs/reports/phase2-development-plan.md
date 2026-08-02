# 二期开发计划（Phase 4 收尾 + Phase 5 上线）

> **文档定位**：一期（Phase 0-3）已交付 20 个微服务 + 5 个前端 + YARP 网关（v7.3）。本计划汇总全部剩余任务，作为二期（Phase 4 收尾 + Phase 5）的执行蓝图。
> **编制**：摩登时代 · 2026-08-02 · 对应 PROJECT_PLAN.md v4.1 第 19-22 周
> **当前基线**：v7.3（Week 18 完成：秒杀 ✅ / 缓存优化 ✅ / 限流熔断 ✅ / 分库分表评估 ✅）

---

## 一、总览

| 阶段 | 周次 | 主题 | 核心产出 |
|---|---|---|---|
| 二期-1 | Week 19 | **全量压测 + 瓶颈优化** | 压测报告（reports/）+ 优化清单 |
| 二期-2 | Week 20 | **部署上线（IIS / Windows Service）** | 一键启动脚本 + 部署文档 + 本机 IIS 部署 |
| 二期-3 | Week 21 | **运维体系（监控/日志/链路追踪）** | 告警规则 + 日志归档 + 链路可视化 |
| 二期-4 | Week 22 | **灰度发布 + 全量上线** | 上线方案 + 正式运行 |

---

## 二、二期-1：全量压测 + 瓶颈优化（Week 19）

### 2.1 目标
- 基于 Week 18 已就绪的基础设施（Redis 缓存 / 网关限流 / Polly 弹性 / 分库分表评估报告），对全链路做一次真实压测，拿到吞吐/延迟基线。

### 2.2 任务清单

| # | 任务 | 说明 | 产出 |
|---|---|---|---|
| 1 | **performance-service 全量压测** | 用 8017 自带压测引擎对核心链路压测：网关→identity 登录、product 商品列表/详情（缓存命中 vs 未命中）、order 下单、promotion 秒杀（Redis 预扣） | HTML 压测报告 |
| 2 | **压测基线落库** | 记录各服务 QPS/RT/P99/错误率，作为后续优化对照 | reports/loadtest-*.html |
| 3 | **瓶颈定位** | 按压测结果定位：DB 慢查询 / 网关限流阈值 / 缓存命中率 / 连接池 | 优化清单 |
| 4 | **瓶颈优化** | 按需：索引补充、连接串池化、缓存 TTL 调整、限流配额校准 | 代码变更 |
| 5 | **表分区启动条件确认** | 压测后评估各表数据量：达阈值则按 `docs/database/sharding-partition-templates.sql` 落地 Orders/LogEntries 按月分区 | DDL + 维护作业 |

### 2.3 验收标准
- 压测报告含 5 条以上核心链路数据
- 明确列出 Top 3 瓶颈及优化方案

---

## 三、二期-2：部署上线（Week 20）

### 3.1 部署形态决策
| 方案 | 说明 | 决策 |
|---|---|---|
| **IIS 托管（本机，当前执行）** | ASP.NET Core Module v2 托管 20 服务 + 网关；前端静态站点 | ⭐ 本机开发/演示首选（本期执行） |
| Windows Service | sc create / NSSM 注册系统服务 | 备选（IIS 之外的常驻方案） |
| K8s | 容器化编排 | 远期（资源允许时） |

### 3.2 任务清单

| # | 任务 | 说明 | 产出 |
|---|---|---|---|
| 1 | **安装 .NET 10 Hosting Bundle** | IIS 托管 .NET 应用的 ANCM v2 前置 | aspnetcore.dll 就绪 |
| 2 | **安装 URL Rewrite + ARR** | 前端静态站点 /api 转发到网关 8000 | rewrite.dll + arr 就绪 |
| 3 | **一键启动脚本 `scripts/start-all.ps1`** | 本机非 IIS 场景：批量启动 21 个进程 + 健康检查 | 脚本 |
| 4 | **部署文档 `docs/guides/local-deployment.md`** | 本机部署全流程（IIS + 直跑双模式） | 文档 |
| 5 | **Aspire AppHost 服务注册补全** | 现仅 8 服务 → 补全至 21（AppHost.cs + slnx + ProjectReference） | 代码 |
| 6 | **IIS 站点创建** | 21 个后端站点 + 4 个前端站点（见部署清单） | 站点就绪 |
| 7 | **前端生产构建** | web-customer/web-merchant/web-admin/mobile-app 的 `vite build` | dist/ |
| 8 | **部署验证冒烟** | 网关健康 + 各站点首页 + 登录链路 | 冒烟记录 |

### 3.3 IIS 部署清单（本机）

| 站点 | 类型 | 端口 | 物理路径 |
|---|---|---|---|
| mmp-gateway | 后端 | 8000 | E:\IISDeploy\gateway |
| mmp-identity … mmp-bi-admin | 后端 × 19 | 8001-8020 | E:\IISDeploy\services\{name} |
| mmp-web-customer | 前端 | 5173 | E:\IISDeploy\web\web-customer |
| mmp-web-merchant | 前端 | 5174 | E:\IISDeploy\web\web-merchant |
| mmp-web-admin | 前端 | 5177 | E:\IISDeploy\web\web-admin |
| mmp-mobile | 前端 | 5175 | E:\IISDeploy\web\mobile-app |

> desktop-app 为 Electron 桌面程序，不做 IIS 部署（本地打包分发）。

---

## 四、二期-3：运维体系（Week 21）

| # | 任务 | 说明 | 产出 |
|---|---|---|---|
| 1 | **监控告警完善** | performance-service 8017 监控接入告警规则：CPU>80% / 内存>85% / 错误率>5% / 5xx 突增 | 告警配置 |
| 2 | **日志归档** | logging-service 8011 日志按月归档（配合表分区模板），保留策略 6 个月 | 归档作业 |
| 3 | **链路追踪** | OpenTelemetry 已引用（Directory.Packages.props）→ 接入 Jaeger/OTLP 或 Console 导出，跨服务 trace 串联 | 链路数据 |
| 4 | **Redis 监控** | info/monitor 采样 + 内存水位（512mb noeviction 告警） | 监控脚本 |

---

## 五、二期-4：灰度发布 + 全量上线（Week 22）

| # | 任务 | 说明 | 产出 |
|---|---|---|---|
| 1 | **灰度发布方案** | 网关路由级灰度：按商户/按用户比例切流（YARP 路由权重） | 方案文档 |
| 2 | **回滚预案** | 每服务保留上一版本目录 + 切换脚本（IIS 站点指向原子切换） | 预案 |
| 3 | **上线清单** | 环境核对（连接串/密钥/Redis/防火墙）、备份策略、验收标准 | Checklist |
| 4 | **正式上线** | 全量切换 + 观察窗口 + 上线报告 | 上线报告 |

---

## 六、遗留工程化项（贯穿二期）

| # | 任务 | 优先级 | 说明 |
|---|---|---|---|
| 1 | scripts/start-all.ps1 一键启动 | 高 | 直跑模式批量启动 + 健康检查（IIS 部署后降为备选） |
| 2 | Aspire AppHost 补全至 21 服务 | 中 | slnx + ProjectReference + AppHost.cs 三处同步 |
| 3 | docs/guides/local-deployment.md | 高 | 本机部署双模式文档 |
| 4 | 旧密钥 GitHub 历史清理 | 低 | 仓库私有、风险可控；彻底清除需 git filter-repo + force push（唯一使用者先行备份） |
| 5 | 表分区落地（数据量达标后） | 中 | 按 db-sharding-evaluation.md 方案 A |
| 6 | 消息延迟投递（ScheduledAt） | 低 | 秒杀开始前预热等场景可选 |

---

## 七、里程碑与时间线

| 里程碑 | 时间 | 交付物 |
|---|---|---|
| M1：压测基线 | Week 19 中 | 压测报告 |
| M2：本机 IIS 部署完成 | Week 20 中 | 21 后端 + 4 前端站点可访问 |
| M3：运维体系就绪 | Week 21 末 | 告警/归档/链路 |
| M4：全量上线 | Week 22 末 | 上线报告 |

## 八、风险与依赖

| 风险 | 等级 | 缓解 |
|---|---|---|
| IIS ANCM 与 .NET 10 兼容性 | 中 | 使用官方 Hosting Bundle 10.0.10 匹配当前 SDK |
| 端口冲突（8000-8020 / 5173-5177 已占用） | 低 | 部署前停旧进程，IIS 站点独占绑定 |
| 前端 /api 转发依赖 URL Rewrite + ARR | 中 | 本期一并安装两模块 |
| 压测暴露真实瓶颈 | 中 | 预留 Week 19 整周优化时间 |

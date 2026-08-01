# 多商户入驻电商平台（MultiMerchantPlatform）

> **摩登时代** 出品 · .NET 10 微服务架构

## 项目简介

多商户入驻的 B2C 电商平台（类似淘宝/京东模式），支持商户入驻、商品管理、订单交易、支付结算、即时通讯等全流程。

## 技术栈

| 层 | 技术 |
|----|------|
| 后端 | .NET 10 + ASP.NET Core + YARP + .NET Aspire |
| ORM | EF Core 10 / SqlSugar / Dapper（可切换） |
| 数据库 | SQL Server 2022 |
| 前端 | Vue 3.5 + Vite 8 + TypeScript + Element Plus + uni-app + Electron |
| 消息队列 | 自封装 messaging-service |
| 日志 | 自封装 logging-service |

## 项目结构

```
src/
├── BuildingBlocks/     # 公共基础组件（8 个）
├── services/           # 微服务（21 个）
├── gateways/           # API 网关（YARP）
├── apps/               # 前端应用（Vue 3 五端 + shared 共享层）
└── AspireHost/         # .NET Aspire 编排宿主
```

## 快速开始

```bash
# 1. 确保已安装 .NET 10 SDK
dotnet --version  # 应显示 10.x

# 2. 还原依赖
dotnet restore

# 3. 启动 Aspire 编排（启动所有服务）
cd src/AspireHost/AspireHost.AppHost
dotnet run

# 4. 访问网关
# http://localhost:8000
```

## 文档

- [项目方案](docs/PROJECT_PLAN.md)
- [变更记录](docs/CHANGELOG.md)
- [文档索引](docs/DOC_INDEX.md)

## 分支策略

| 分支 | 用途 |
|------|------|
| `main` | 生产稳定版本 |
| `dev` | 开发集成分支 |
| `feature/*` | 功能开发分支 |
| `fix/*` | Bug 修复分支 |

## License

Copyright © 2026 摩登时代. All rights reserved.

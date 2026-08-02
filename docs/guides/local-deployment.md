# 本机 IIS 部署指南

> **版本**：v7.3 · 2026-08-02 · Phase 4 Week 20 前置
> **部署形态**：IIS 托管（21 后端 + 4 前端站点）· 本机 Windows + IIS 10
> **部署目录**：`E:\IISDeploy\`（services/ 后端、web/ 前端）

---

## 一、前置条件

| 组件 | 版本 | 检查命令 |
|---|---|---|
| Windows IIS | 10.0（W3SVC 运行中） | `Get-Service W3SVC` |
| .NET 10 Hosting Bundle | 10.0.10（含 ANCM v2） | `Test-Path C:\Windows\System32\inetsrv\aspnetcore.dll`（实际在 Program Files\dotnet 下） |
| URL Rewrite 2.1 | 7.2.1993 | `Test-Path C:\Windows\System32\inetsrv\rewrite.dll` |
| ARR 3.0 | 3.0.5311 | IIS 管理器 → 服务器节点 → Application Request Routing Cache |
| 管理员权限 | — | 必须（安装组件/建站点/建应用池） |

> 安装包已存 `tools/iis-deploy/`（dotnet-hosting-10.0.10-win.exe / rewrite_2.1_rtw_x64.msi / requestRouter_amd64.msi）。
> 推荐用 winget：`winget install Microsoft.IIS.URLRewrite Microsoft.IIS.ApplicationRequestRouting`。

---

## 二、部署步骤

### 1. 发布后端（21 个服务 + 网关）

```powershell
# 逐个发布（或运行 scripts/publish-all.ps1）
dotnet publish src/services/identity-service/IdentityService.csproj -c Release -o "E:/IISDeploy/services/identity-service"
dotnet publish src/gateways/ApiGateway/ApiGateway.csproj -c Release -o "E:/IISDeploy/services/gateway"
# ... 其余 19 个服务同理
```

> 注意：发布目标目录需预先存在（`New-Item -ItemType Directory -Force`）；若报 Access denied，删目录重建后再发。

### 2. 构建前端（4 个应用）

```powershell
cd src/apps/web-customer && npm run build          # dist/ → E:\IISDeploy\web\web-customer
cd src/apps/web-merchant && npm run build          # → web-merchant
cd src/apps/web-admin && npm run build             # → web-admin
cd src/apps/mobile-app && npm run build:h5         # dist/build/h5 → mobile-app
```

### 3. 创建 IIS 站点

```powershell
powershell -ExecutionPolicy Bypass -File scripts/create-iis-sites.ps1
```

脚本自动完成：创建 25 个应用池（No Managed Code + AlwaysRunning）→ 创建 25 个站点 → 前端站点注入 web.config（/api、/hub 反向代理到网关 8000）→ 启用 ARR 代理。

### 4. 验证

```powershell
# 后端：全部 /api/health 应返回 200
for ($p in 8000..8020) { curl -s -o /dev/null -w "$p %{http_code}`n" http://localhost:$p/api/health }
# 前端：首页 200 + /api 代理可用
curl http://localhost:5173/          # web-customer
curl -X POST http://localhost:5173/api/identity/auth/register -H "Content-Type: application/json" -d '{"email":"t@t.com","password":"Test123456","displayName":"t"}'
```

---

## 三、站点清单与访问地址

### 后端（21 站点，全部 http://localhost:{port}）

| 站点 | 端口 | 说明 |
|---|---|---|
| mmp-gateway | 8000 | YARP 网关（入口） |
| mmp-identity | 8001 | 认证/注册/JWT |
| mmp-merchant | 8002 | 商户入驻 |
| mmp-product | 8003 | 商品/分类 |
| mmp-order | 8004 | 订单 |
| mmp-pay | 8005 | 支付 |
| mmp-stock | 8006 | 库存 |
| mmp-cart | 8007 | 购物车 |
| mmp-search | 8008 | 搜索 |
| mmp-promotion | 8009 | 营销/秒杀 |
| mmp-messaging | 8010 | 消息总线 |
| mmp-logging | 8011 | 日志 |
| mmp-review | 8012 | 评价 |
| mmp-logistics | 8013 | 物流 |
| mmp-settlement | 8014 | 结算 |
| mmp-email | 8015 | 邮件 |
| mmp-im | 8016 | 即时通讯 |
| mmp-performance | 8017 | 压测/监控 |
| mmp-risk | 8018 | 风控 |
| mmp-notification | 8019 | 通知中心 |
| mmp-bi-admin | 8020 | BI 分析 |

### 前端（4 站点）

| 站点 | 端口 | 访问地址 | 说明 |
|---|---|---|---|
| mmp-web-customer | 5173 | http://localhost:5173/ | C 端商城 |
| mmp-web-merchant | 5174 | http://localhost:5174/ | 商户端 |
| mmp-mobile | 5175 | http://localhost:5175/ | 移动端 H5 |
| mmp-web-admin | 5177 | http://localhost:5177/ | 管理后台 BI |

> desktop-app（Electron）为桌面程序，不部署 IIS。

---

## 四、关键配置说明

### 前端 /api 转发
前端生产构建的 axios baseURL 为 `/api`（相对路径），IIS 站点 web.config 用 URL Rewrite 将 `/api/*` 转发到网关 `http://localhost:8000/api/*`，`/hub/*`（SignalR WebSocket）同理。依赖 ARR 代理（`system.webServer/proxy enabled=true`）。

### 应用池
- 每个站点独立应用池（**ASP.NET Core 不支持多应用共池**，否则 500.35）
- No Managed Code + AlwaysRunning（防空闲回收）

### web.config（后端，发布自动生成）
```xml
<aspNetCore processPath="dotnet" arguments=".\IdentityService.dll" hostingModel="inprocess" />
```

---

## 五、常见问题

| 症状 | 原因 | 处理 |
|---|---|---|
| 500.35 | 多应用共用一个应用池 | 每站点独立池：`appcmd set app "站点/" /applicationPool:站点名` |
| 500.19 配置错误 | web.config 语法/重复项 | 移除重复 mimeMap（IIS 10 默认已含） |
| 404.4 | rewrite 后无处理器 | 启用 ARR：`appcmd set config -section:system.webServer/proxy /enabled:"True" /commit:apphost` |
| /api/health 404 | 该服务无此端点 | 服务各有 HealthController（路径可能不同），以业务接口为准 |
| Swagger 404 | 仅开发环境启用 | 生产环境属正常 |
| Access denied（发布） | 目录残留/权限 | 删目录重建，robocopy 兜底 |
| 端口占用 | 旧直跑进程 | `Get-NetTCPConnection -LocalPort X` → Stop-Process |

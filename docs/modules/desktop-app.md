# desktop-app 模块文档（Electron 桌面端 · v6.6）

> 版本：v6.6 · 2026-08-02 · 对应 Phase 3 Week 15-16「桌面端 Electron」

## 一、职责概述

**摩登商户工作台** — Windows + macOS 桌面应用（Electron 33 + Vue 3.5），商户/平台用户的桌面工作入口。
本阶段（v6.6）聚焦**内部消息能力**：

- **平台公告**：公告中心（列表 / 详情 / 已读 / 未读角标），数据源 notification-service（v6.6 新增公告模块）
- **内部邮件**：内部邮件中心（写邮件 / 收件箱 / 详情 / 失败重试），数据源 email-service（DryRun 模式，不真实外发 SMTP）
- **通知收件箱**：站内信（列表 / 已读 / 全部已读 / 删除 / 未读角标）+ SignalR 实时推送
- 实时通道：`/hub/notification` 推送新公告 / 新站内信 / 未读数变化

**范围边界**：短信 / Push 生产网关**暂不接入**（保持 DryRun），外部邮件 SMTP 暂不启用 —— 仅内部公告 + 内部邮件。

## 二、技术架构

```
┌──────────────────────────────────────────────────────────┐
│                  Electron 主进程 (electron/main.ts)       │
│  创建 BrowserWindow(1280×800) · preload 最小暴露 ·        │
│  外部链接走系统浏览器 · file:// 加载 dist/index.html      │
├──────────────────────────────────────────────────────────┤
│              Electron 渲染进程（Vue 3.5 + Vite 8）        │
│  ┌──────────┬────────────┬────────────┬────────────────┐ │
│  │ 公告中心  │ 内部邮件    │ 通知收件箱 │ 工作台首页      │ │
│  │ List/Detail│Inbox/Compose│List/已读  │ 三栏概览+角标  │ │
│  └────┬─────┴─────┬──────┴─────┬──────┴───────┬────────┘ │
│       │ axios(/api)│            │ SignalR(/hub)│          │
│       └──────┬────┴────────────┴──────┬────────┘          │
├──────────────┼────────────────────────┼───────────────────┤
│   YARP 网关 8000（/api/notifications · /api/emails ·      │
│   /api/identity · /api/merchant · /hub/notification）     │
└──────────────────────────────────────────────────────────┘
```

- 目录：`src/apps/desktop-app/`
- 技术栈：Electron 33 + Vue 3.5 + Vite 8 + TypeScript 5.8 + Element Plus 2.10 + Pinia 3 + Vue Router 4 + Axios + @microsoft/signalr
- 渲染 dev 端口：**5176**（Vite 代理 `/api`、`/hub(ws)` → 网关 8000）
- 路由模式：**hash**（Electron file:// 协议下 history 模式不可用）
- 构建：`vite build`（渲染 → `dist/`）+ `tsc -p electron`（主进程 → `dist-electron/`），`electron-builder` 打包 Win（nsis）+ Mac（dmg）

## 三、工程结构

```
desktop-app/
├── electron/
│   ├── main.ts            # 主进程：窗口创建、dev/prod 加载、外链拦截
│   ├── preload.ts         # 预加载：contextBridge 暴露 window.desktop.appInfo（最小面）
│   └── tsconfig.json      # 主进程 TS（CommonJS → dist-electron/）
├── src/                   # 渲染进程
│   ├── api/
│   │   ├── http.ts        # Axios 封装：注入 JWT + X-Merchant-Id，401 跳登录
│   │   ├── auth.ts        # /identity/auth/login + /merchant/merchants/me
│   │   ├── announcements.ts # 公告 API + DTO
│   │   ├── emails.ts      # 邮件 API + DTO
│   │   └── notifications.ts # 站内信 API + DTO
│   ├── signalr/notification.ts  # Hub 连接（query access_token）+ 事件注册
│   ├── stores/auth.ts     # token / user / merchant 状态
│   ├── router/index.ts    # hash 路由 + 登录守卫
│   └── views/
│       ├── Login.vue               # 登录（经网关）
│       ├── Dashboard.vue           # 工作台首页（最新公告/通知/邮件三栏）
│       ├── layout/MainLayout.vue   # 侧边栏 + 顶栏（公告/通知未读角标、退出）
│       ├── announcements/List.vue · Detail.vue
│       ├── emails/Inbox.vue · Compose.vue
│       └── notifications/Inbox.vue
├── vite.config.ts         # 渲染构建（base './'，代理网关）
├── electron-builder.yml   # 打包配置（Win nsis / Mac dmg）
└── package.json           # scripts: dev / build / dist
```

## 四、功能清单

| 模块 | 功能 | 实现要点 |
|------|------|----------|
| 登录 | 邮箱+密码 → JWT → 商户信息 | `/identity/auth/login`；未入驻 204 不阻断 |
| 公告中心 | 分类筛选 / 分页 / 未读角标 / 详情 / 标记已读 | `/notifications/announcements*`；SignalR 新公告实时角标+1 |
| 内部邮件 | 写邮件 / 收件箱 / 详情（含正文）/ 失败重试 | `/emails`（email-service，DryRun 落库）；列表与详情返回 `body` |
| 通知收件箱 | 列表 / 单条已读 / 全部已读 / 删除 / 未读角标 | `/notifications*`；SignalR `ReceiveNotification`/`UnreadCountChanged` |
| 实时通道 | 连接/自动重连/登出断开 | `/hub/notification?access_token=`；WebSocket 不能带 Authorization 头 |

## 五、与网关/服务的对接

| 服务 | 网关路径 | 直连端口 |
|------|----------|----------|
| identity-service | `/api/identity/**`（前缀剥离） | 8001 |
| merchant-service | `/api/merchant/**`（前缀剥离） | 8002 |
| notification-service | `/api/notifications/**` / `/hub/notification/**` | 8019 |
| email-service | `/api/emails/**`（**无前缀剥离**，v6.6 修复） | 8015 |

> ⚠️ v6.6 网关修复：email-service 路由原带 PathPattern 前缀剥离，导致 `/api/emails`（无子路径）被转发成 `/api` → 404。
> email-service 控制器自带 `api/[controller]` 前缀，与 cart/search 一致**不做前缀剥离**。

## 六、运行与构建

```bash
cd src/apps/desktop-app
npm install            # Electron 二进制下载较慢
npm run dev            # 开发：vite(5176) + tsc(主进程) + electron 启动
npm run build          # 生产构建：dist/ + dist-electron/
npm run dist           # electron-builder 打包（Win nsis / Mac dmg → release/）
```

依赖后端：identity 8001 / merchant 8002 / email 8015 / notification 8019 / gateway 8000（先启动再打开桌面端）。

## 七、冒烟测试

- `tests/smoke-announcement.sh` — 公告模块 API 冒烟（27 项）
- `tests/smoke-desktop-app.sh` — 桌面端端到端冒烟（经网关：登录 → 公告 → 邮件 → 通知 → 鉴权，19 项）

## 八、已知边界

- 邮件正文由 email-service 落库（DryRun 不真实外发），列表/详情接口返回 `body` 字段（v6.6 补强）
- 短信 / Push 真实网关、外部 SMTP 外发：**暂缓**（扩展点已就绪，见 notification-service / email-service 模块文档）
- Electron 二进制与 electron-builder 打包产物体积较大，首次 `npm install` 耗时较长
- **Electron 二进制下载**：国内网络建议镜像加速 `ELECTRON_MIRROR=https://npmmirror.com/mirrors/electron/`；
  若 npm install 已完成但 `dist/electron.exe` 缺失，可手动 `ELECTRON_MIRROR=... node node_modules/electron/install.js`
- **沙箱/自动化环境启动**：若环境设置了 `ELECTRON_RUN_AS_NODE=1`，electron.exe 会以 Node 模式运行
  （`require('electron')` 返回路径字符串 → `app undefined`）；需 `env -u ELECTRON_RUN_AS_NODE electron .` 启动。
  验证渲染成功：主进程日志输出 `[desktop-app] renderer loaded: 摩登商户工作台`（did-finish-load），
  异常输出 `renderer gone`（render-process-gone）
- 主进程 dev 加载 Vite dev server（`VITE_DEV_SERVER_URL`），生产 `loadFile(dist/index.html)`；hash 路由兼容 file://

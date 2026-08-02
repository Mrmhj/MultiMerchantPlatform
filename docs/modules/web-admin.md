# web-admin 模块文档

> **文档路径**：`docs/modules/web-admin.md`
> **版本**：v7.0 · 2026-08-02 · **端口 5177（dev，Vite 代理 → 网关 8000）**
> **定位**：平台管理后台 — BI 数据看板（Vue 3.5 + Vite 8 + TS + Element Plus + ECharts 5）

---

## 一、职责概述

| 能力 | 说明 |
|------|------|
| **管理员登录** | identity-service 登录（JWT），仅 admin 角色可访问看板（403 拦截提示） |
| **BI 看板** | 核心指标卡 + 销售趋势 + 商户/商品排行 + 订单状态分布（ECharts 图表） |
| **数据同步** | 「同步数据」按钮触发 bi-admin `POST /api/bi/sync`，成功后自动刷新全部图表 |
| **趋势切换** | 近 7 / 30 / 90 天销售趋势切换（前端发起 days 参数） |

---

## 二、技术栈与工程结构

```
web-admin/
├── index.html                    # 平台管理后台标题
├── package.json                  # vue/element-plus/echarts/pinia/vue-router/axios
├── vite.config.ts                # 端口 5177；/api 代理 → http://localhost:8000（YARP 网关）
├── tsconfig.json                 # TS strict + @/* 别名
└── src/
    ├── main.ts                   # ElementPlus(zh-cn) + Pinia + Router
    ├── App.vue                   # 路由出口
    ├── api/
    │   ├── http.ts               # Axios 封装（JWT 注入 / 401 跳登录 / 403 提示 / 统一错误）
    │   └── index.ts              # authApi（登录）+ biApi（五类看板接口 + sync）
    ├── stores/auth.ts            # Pinia：token + user（setSession/logout）
    ├── router/index.ts           # /login + /（AdminLayout 下 dashboard）；登录守卫
    ├── layouts/AdminLayout.vue   # 深色侧边栏 + 顶栏（管理员信息 / 退出）
    └── views/
        ├── Login.vue             # 管理员登录页
        └── Dashboard.vue         # BI 看板（核心图表页面）
```

---

## 三、Dashboard 看板布局

| 区域 | 图表 | 数据源（bi-admin） |
|------|------|-------------------|
| 指标卡 × 6 | GMV / 订单总数 / 已完成 / 商户数 / 商品数 / 用户数 | `GET /api/bi/overview` |
| 销售趋势 | 双轴折线（GMV 左轴 + 订单数右轴，面积填充） | `GET /api/bi/sales-trend?days=7/30/90` |
| 商户排行 | 横向条形图 TOP10（GMV 降序） | `GET /api/bi/merchant-rank?top=10` |
| 商品排行 | 横向条形图 TOP10（销售额降序） | `GET /api/bi/product-rank?top=10` |
| 状态分布 | 环形饼图（待付款/已付款/已完成/已取消） | `GET /api/bi/order-status` |

- 图表渲染：`echarts.init` + `setOption`（组件挂载后初始化，`window.resize` 自动 resize，卸载时 dispose）
- 数据加载：`Promise.allSettled` 并发拉取五路，任一失败不影响其余图表
- 同步：`onSync` → `POST /api/bi/sync` → 成功后 `loadAll()` 刷新

---

## 四、认证与权限

- 登录：`POST /api/identity/auth/login`（经网关）→ 保存 token + user 到 Pinia/localStorage
- 守卫：未登录访问任意页 → 跳 `/login?redirect=...`；已登录访问 login → 跳 dashboard
- 403：非 admin 角色访问 `/api/bi/**` → 拦截器提示「无权限访问（需要管理员账号）」
- 开发环境 admin 提权：`UPDATE Users SET RolesJson='["admin"]' WHERE Email='...'`（本地开发库，见冒烟脚本）

---

## 五、运行方式

```bash
cd src/apps/web-admin
npm install          # 首次（依赖：vue/element-plus/echarts 等）
npm run dev          # http://localhost:5177（5177 被占用时 Vite 自动 +1）
npm run build        # 产物 dist/（tsc 类型检查由 build 前 vite build 完成）
```

> 依赖后端：identity 8001 / merchant 8002 / product 8003 / order 8004 / pay 8005 / stock 8006 / bi-admin 8020 / 网关 8000 已启动。

---

## 六、冒烟覆盖（tests/smoke-bi.sh 后端侧）

- 前端视觉验证：dev 启动 200 + 页面标题「多商户商城 - 平台管理后台」
- 后端五类接口 + 同步 + 鉴权由 `smoke-bi.sh` 覆盖（31/31 通过）

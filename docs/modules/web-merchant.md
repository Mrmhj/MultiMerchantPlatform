# web-merchant 商户端 Web

> 模块文档 · 摩登时代 · 2026-08-02 · Phase 2 Week 12-13

## 一、概述

| 项 | 值 |
|---|---|
| 目录 | `src/apps/web-merchant/` |
| 技术栈 | Vue 3.5 + Vite 8 + TypeScript 5.x + Element Plus 2.x + Pinia + Vue Router 4 + Axios |
| 端口 | **5174**（dev），Vite 代理 `/api`、`/hub`（WebSocket）→ YARP 网关 8000 |
| 定位 | 商户管理后台：入驻 → 商品 → 订单 → 库存 → 营销 → 评价 → 物流 → 结算 → 在线客服 |
| 鉴权 | JWT（`/identity/auth/login` 经网关）+ 全部业务请求自动携带 `X-Merchant-Id` 头 |

**说明**：与 C 端商城（web-customer 5173）同栈同构，共享 Element Plus 组件生态；登录即拉取商户状态（`/merchant/merchants/me`），未入驻自动引导入驻，审核通过前业务页面提示先行入驻。

## 二、目录结构

```
web-merchant/
├── vite.config.ts          # 5174 + /api 与 /hub(ws) 代理
├── src/
│   ├── main.ts             # Pinia + Router + Element Plus（zh-CN）
│   ├── App.vue
│   ├── api/
│   │   ├── http.ts         # Axios 封装：JWT + X-Merchant-Id 注入，401 跳登录，统一错误提示
│   │   └── index.ts        # 10 个服务 API 客户端 + 类型定义
│   ├── stores/auth.ts      # token / 商户信息 / 入驻状态（1待审 2通过 3驳回 4禁用）
│   ├── router/index.ts     # 登录守卫 + 12 个页面路由
│   ├── layouts/MerchantLayout.vue  # 侧边栏导航（9 个模块）+ 顶栏（商户名/退出）
│   └── views/
│       ├── Login.vue / MerchantApply.vue / Dashboard.vue
│       ├── products/  Products.vue · ProductEdit.vue（SKU 编辑器）· Categories.vue
│       ├── orders/    Orders.vue · OrderDetail.vue（发货/确认完成）
│       ├── stocks/    Stocks.vue（补货/流水）
│       ├── marketing/ Promotions.vue（优惠券 + 满减活动双 Tab）
│       ├── reviews/   Reviews.vue（回复/隐藏）
│       ├── logistics/ Shipments.vue（运单 + 轨迹时间线）
│       ├── settlements/ Settlements.vue（概览卡片 + 明细）
│       └── im/        ImChat.vue（SignalR 客服聊天）
```

## 三、功能清单

| 模块 | 页面 | 功能 | 依赖服务（网关路径） |
|---|---|---|---|
| 登录 | Login | 邮箱密码登录（经网关 `/identity/auth/login`） | identity |
| 入驻 | MerchantApply | 提交申请 / 审核中 / 驳回重提 / 通过后引导；已通过复用资料 | merchant `/merchant/**` |
| 工作台 | Dashboard | 结算概览（待结算单数/金额、累计结算、佣金比例）+ 商户信息 | settlement |
| 商品 | Products / ProductEdit / Categories | 列表（状态/关键词筛选）+ 上下架；创建/编辑（名称/分类/封面/描述 + SKU 表格编辑器）；分类 CRUD | product |
| 订单 | Orders / OrderDetail | 子订单列表（状态筛选）+ 详情 + 发货（物流公司下拉 + 运单号，自动建运单）+ 确认完成 | order + logistics |
| 库存 | Stocks | 列表（总/预占/可用）+ 补货 + 流水弹窗（创建/预占/扣减/释放/补货） | stock |
| 营销 | Promotions | 满减活动（草稿→启用/停用）+ 优惠券（总量/限领/有效期，启停） | promotion |
| 评价 | Reviews | 列表（状态/评分筛选）+ 回复（≤500）+ 隐藏/恢复 | review |
| 物流 | Shipments | 运单列表（状态筛选）+ 轨迹时间线（状态/描述/地点/时间） | logistics |
| 结算 | Settlements | 概览卡片 + 结算单列表（周期/金额/状态）+ 明细（订单级）+ 佣金比例标签 | settlement |
| 客服 | ImChat | 会话列表（未读数）+ 聊天窗口；**SignalR 实时**（收发/已读回执/输入中指示，`/hub/chat`）+ REST 兜底 | im |

## 四、关键设计

1. **X-Merchant-Id 自动注入**：`http.ts` 请求拦截器从 `localStorage.merchantId`（登录后 `merchants/me` 写入）自动加头，所有商户接口零重复代码
2. **登录守卫**：未登录 → `/login`（带 redirect 回跳）；已登录访问登录页 → 重定向工作台
3. **入驻状态门禁**：商户状态非「通过」时，Dashboard 展示入驻引导，业务页仍可访问但接口按服务端 400 `MERCHANT_REQUIRED` 提示
4. **IM 双通道**：优先 SignalR（`invoke('SendMessage')` 实时双向），连接失败自动回退 REST `reply`；收到其他会话消息自动刷新列表未读数；上线自动补推离线消息（服务端行为）
5. **Vite 代理**：`/api` → 8000（REST），`/hub` → 8000（`ws: true` 支持 WebSocket）

## 五、测试与验证

- `vite build` 构建通过 + `tsc --noEmit` 0 错误（含 `env.d.ts` Vue SFC 声明）
- 冒烟 `tests/smoke-web-merchant.sh`（21 项，经 Vite 代理模拟浏览器真实链路）：注册/登录 → 未入驻 204 → 入驻申请 → admin 提权审核通过 → 商户回查 → 分类/商品创建上架 → 库存/优惠券/活动/评价/运单/结算概览/佣金 → IM 内部推送建会话 → 商户会话列表 + 回复，全通过

## 六、后续增强

- 商品 SKU 编辑（增删改同步库存）已接入服务端能力，编辑页可扩展；订单批量导出
- 聊天页已读回执 UI 增强（对方已读标记）；未读角标轮询（当前依赖 SignalR 事件刷新）
- 接入 web-admin 的商户审核提示（通知中心上线后）

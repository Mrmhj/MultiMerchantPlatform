# mobile-app 移动端（uni-app）

> 模块文档 · 摩登时代 · 2026-08-02 · Phase 2 Week 13

## 一、概述

| 项 | 值 |
|---|---|
| 目录 | `src/apps/mobile-app/` |
| 框架 | uni-app（Vue 3 语法，`3.0.0-5010520260709002` 内核），一套代码编译 iOS / Android（App）+ H5 + 小程序 |
| 端口 | **5175**（H5 dev），Vite 代理 `/api`、`/hub`(ws) → YARP 网关 8000 |
| 状态管理 | Pinia（auth store：token / 用户信息本地持久化） |
| 请求封装 | `uni.request` 统一（JWT 注入、baseURL、401 跳登录、错误 toast） |
| 定位 | C 端移动商城骨架：首页浏览/搜索 → 商品详情 → 购物车 → 下单 → 模拟支付 → 订单 → 在线客服（SignalR） |

**说明**：基于官方模板 `dcloudio/uni-preset-vue#vite-ts` 精简（仅保留 h5 + app 平台依赖），页面与 web-customer 同走 YARP 网关，JWT 认证统一。

## 二、目录结构

```
mobile-app/
├── vite.config.ts          # 5175 + /api 与 /hub(ws) 代理
├── tsconfig.json           # 内联配置（模板 @vue/tsconfig 与新 TS 不兼容，已移除 extends）
├── src/
│   ├── main.ts             # createSSRApp + Pinia
│   ├── App.vue             # onLaunch 恢复登录态 + 全局样式
│   ├── manifest.json       # App 配置（name/appid/权限，Android 精简权限）
│   ├── pages.json          # 8 页面 + tabBar（首页/购物车/订单/我的）
│   ├── api/
│   │   ├── http.ts         # uni.request 封装（JWT + baseURL + 401/错误处理）
│   │   └── index.ts        # 认证/商品/搜索/购物车/订单/支付/IM 买家接口
│   ├── stores/auth.ts      # token + 用户信息（uni storage 持久化）
│   └── pages/
│       ├── index/index     # 首页：搜索 + 分类入口 + 商品列表（分页）
│       ├── product/detail  # 详情：SKU 选择 + 数量步进 + 加购/立即购买
│       ├── cart/cart       # 购物车：多商户分组/勾选/数量/删除 + 结算
│       ├── order/list      # 订单列表：状态 Tab + 分页
│       ├── order/detail    # 详情：商品 + 取消 + 模拟支付 + 联系客服
│       ├── login/login     # 登录/注册
│       ├── profile/profile # 个人中心：订单/购物车/客服入口 + 退出
│       └── im/chat         # 客服：会话列表 + 聊天窗口（SignalR 实时）
```

## 三、功能清单

| 页面 | 功能 | 依赖接口（网关） |
|---|---|---|
| 首页 | 商品列表（分页下拉加载）、关键词搜索、分类入口 | product 公开 `/product/products/public`、search `/search/products` |
| 商品详情 | SKU 选择、数量、加入购物车、立即购买 | product 公开详情（含商户名） |
| 购物车 | 多商户分组、全选/单选、数量步进、删除、选中合计、去结算 | cart `/cart/**` |
| 订单 | 状态 Tab（全部/待付/待发/待收/完成）、分页、详情、取消、模拟支付 | order `/order/orders**`、pay `/pay/payments**` |
| 在线客服 | 会话列表（未读数）、聊天窗口、**SignalR 实时收发**（`/hub/chat`，REST 兜底）、已读、发起新会话 | im `/im/**` |
| 个人中心 | 登录态展示、订单/购物车/客服入口、退出登录 | — |

## 四、关键设计

1. **多端统一网关**：H5 经 Vite 代理；App 端可配置 `VITE_API_BASE` 直连网关（骨架默认 `/api` 相对路径）
2. **IM 双通道**：H5 用 `@microsoft/signalr`（`/hub/chat?access_token=`），连接失败自动回退 REST `send`；App 端后续接入原生 WebSocket 封装
3. **加购完整字段**：cart-service 的 `AddCartItemRequest` 需 merchantName/productName/skuCode 等完整字段 → 商品公开接口**已补带 `merchantName`**（product-service 配合改动，调用 merchant 内部接口批量带出）
4. **骨架范围**：支付为模拟支付（pay-service simulate-pay）；微信/支付宝原生支付 API 留待后续；分类页/收藏/物流追踪为后续增强项

## 五、测试与验证

- `build:h5`（uni build）构建通过；`type-check`（vue-tsc）0 错误（修复：tsconfig 移除与 TS 5.4+ 不兼容的 @vue/tsconfig 0.1.3 继承、`uni.request` data 类型收窄）
- 冒烟 `tests/smoke-mobile-app.sh`（15 项，经 5175 代理模拟 App 链路）：注册登录 → 商品列表/搜索 → **补库存（sqlcmd）** → 加购（全字段）→ 购物车 → 下单 → 模拟支付成功 → 订单列表 → IM 创建私聊 → 收发消息/未读数，全通过
- 后端配合改动：product 公开接口（列表+详情）新增 `merchantName` 字段（批量调 merchant 内部接口，失败不阻塞），全量编译 0 警告 0 错误

## 六、后续增强

- App 端原生打包（`build:app`）：微信/支付宝支付、原生 WebSocket 封装、推送
- 分类页、商品评价、物流追踪、收藏、历史/热门搜索
- 移动端 IM 未读角标本地推送；接入 notification-service 站内信

# web-customer — C 端商城（Vue 3 + Vite + Element Plus）

> **所属阶段**：Phase 1 Week 9 · **路径**：`src/apps/web-customer/` · **端口**：5173
> **更新日期**：2026-08-02

## 一、职责

C 端消费者商城（桌面浏览器访问，类似淘宝网页版）：

- 商品浏览（在售商品列表 / 详情 + SKU 选择）
- 用户注册 / 登录（identity-service）
- 下单（直接购买模式，订单自动按商户拆单）
- 支付（模拟支付，全自动：下单 → 支付 → 订单已付款）
- 我的订单（列表 / 详情 / 取消）

## 二、技术栈

| 项 | 版本 | 说明 |
|----|------|------|
| Vue | 3.5.40 | Composition API + `<script setup>` |
| Vite | 8.2.0 | 构建，dev 代理到 YARP 网关 |
| TypeScript | 5.x | 严格模式 |
| Element Plus | 2.14.3 | UI 组件库（自动按需导入） |
| Pinia | 3.x | 状态管理（auth store） |
| Vue Router | 4.x | 路由 + 登录守卫 |
| Axios | 1.x | 统一封装（JWT 注入 / 401 跳登录 / 错误提示） |

## 三、页面结构

```
src/views/
├── Home.vue            # 首页：在售商品卡片网格
├── ProductDetail.vue   # 商品详情：SKU 规格选择 + 数量 + 立即购买
├── Login.vue           # 登录
├── Register.vue        # 注册（注册即登录）
├── OrderSubmit.vue     # 确认订单：提交下单 + 自动支付
├── Orders.vue          # 我的订单列表
└── OrderDetail.vue     # 订单详情：支付 / 取消

src/api/index.ts        # API 客户端（auth/product/order/pay，类型定义）
src/api/http.ts         # Axios 封装（拦截器）
src/stores/auth.ts      # 登录态（Pinia）
src/router/index.ts     # 路由 + 登录守卫
```

## 四、后端对接（经 YARP 网关 8000，dev 由 Vite 代理）

| 前端调用 | 网关路由 | 后端服务 |
|---------|---------|---------|
| `/api/identity/**` | `/api/identity/**` | identity-service |
| `/api/product/**` | `/api/product/**` | product-service |
| `/api/order/**` | `/api/order/**` | order-service |
| `/api/pay/**` | `/api/pay/**` | pay-service |

**C 端商品查询走 product-service 新增公开接口**（无鉴权，仅在售）：
- `GET /api/products/public`（列表）
- `GET /api/products/public/{id}`（详情）

## 五、运行

```bash
cd src/apps/web-customer
npm install
npm run dev        # http://localhost:5173（需先启动后端服务与网关）
npm run build      # 生产构建 → dist/
```

## 六、已验证（端到端）

| 场景 | 结果 |
|------|------|
| 前端构建（Vite 8） | ✅ 成功 |
| 首页公开商品列表（代理→网关→product） | ✅ 2 个在售商品 |
| 注册 / 登录（走网关） | ✅ 201 / token |
| 下单（手撕面包 ×2，预占库存） | ✅ 订单创建 |
| 支付（创建支付单 + 模拟支付） | ✅ 订单 Paid |
| 库存联动（预占→扣减） | ✅ 联动正常 |

## 七、已知限制与后续扩展

- **购物车**：当前为直接购买模式；购物车（cart-service，Phase 2）后接入
- **价格来源**：下单单价由商品接口提供；正式版服务端核价
- **地址/物流**：收货地址、物流追踪待 Phase 2
- **商户/管理端**：web-merchant / web-admin 后续阶段开发（复用 shared 层）
- **移动端**：uni-app（Phase 2），共享 apps/shared 层

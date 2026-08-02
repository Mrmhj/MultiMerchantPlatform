# 变更记录

## [v6.0] - 2026-08-02

### Added
- **Phase 2 Week 12：im-service（8016 即时通讯）**：
  - **SignalR 实时通道**：Hub `/hub/chat`（JWT 鉴权，WebSocket 通过 `access_token` query 携带令牌，`IUserIdProvider` 从 JWT sub 解析用户）；强类型客户端接口 `IChatClient`（ReceiveMessage / MessageRead / TypingIndicator）
  - **会话体系**：私聊（买家 ↔ 商户客服，`GET 或创建` 双向幂等）+ 群聊（商户客服群，成员去重 ≥2 人）；会话状态 Active → Closed；`LastMessageAt/Preview` 列表摘要
  - **消息**：Text / Image / File / OrderCard / System 五类（Content ≤4000）；游标分页历史（`(CreatedAt, Id)` 字典序，最新在前 + hasMore）；未读数统计（买家视角 / 商户视角 = 非商户员工发来的消息）
  - **离线消息**：上线（OnConnected）自动加入全部会话组 + 补推该用户**参与会话内**的未读消息（≤50 条，严格按成员过滤防越权）
  - **已读回执 / 输入中**：`MarkAsRead` 批量置已读 + 群组广播回执；`SendTypingIndicator` 转发（不落库）
  - **内部推送**：`POST /api/im/internal/push`（X-Internal-Key）——订单/物流状态系统通知：指定会话 → 用户最近活跃会话 → 新建「系统通知」会话三级定位，落库 + 实时推送，返回 delivered（是否在线）
  - **多租户**：会话/成员/消息按商户隔离（HasQueryFilter + Handler 显式过滤），缺 `X-Merchant-Id` → 400 `MERCHANT_REQUIRED`；成员校验（非成员发送 → 400 `NOT_SESSION_MEMBER`）
  - **发送者角色权威化**：以会话成员表 Role 为准（Hub 的 JWT 推断仅兜底，未提权客服也能正确落库为 MerchantStaff）
  - 数据库：MMP_IM 库（3 表）+ 网关 `/api/im/**`、`/hub/chat/**` 路由（8016）
  - 新增模块文档 `docs/modules/im-service.md`

### Verified
- 全量编译 0 警告 0 错误（27 个项目，仅环境性 NU1900 缓存警告）
- REST 冒烟全通过（26 项）：健康 → 鉴权 401（无 token / 错误内部密钥）→ 创建私聊 + 幂等同 ID → 发送消息（买家角色 1）→ 空内容 400 → B 未读数 1 → 历史分页 → 已读回执 → 商户 reply（客服角色 2）→ 商户会话列表 → 缺头 400 → 客服群创建 → 内部推送（未在线 delivered=false）→ 通知并入活跃会话
- SignalR 冒烟全通过（9 项）：WebSocket 连接（access_token query 鉴权）→ 双向实时收发（senderRole 正确）→ 输入中指示 → 已读回执 → 非成员发送拒绝（HubException）→ 重连补推离线消息
- 网关链路：`/api/im/**`、`/hub/chat/**` 经 8000 转发正常（401/200/400 均正确透传）

### Notes
- **越权 Bug 修复**：上线补推初版未按用户参与的会话过滤（把全库未读推给新连接）→ 修复为仅推送本人会话内未读，并重测通过
- 设计取舍：连接管理（在线判断）为内存单机方案，集群部署需换 Redis（Phase 4）；已读为整会话粒度（单条回执留待后续）
- 网关无 `/api/auth/**` 路由（identity 注册/登录直连 8001，既有现状，未在本次范围）

---

## [v5.9] - 2026-08-02

### Added
- **Phase 2 Week 11：logistics-service（8013 物流）**：
  - **订单-物流联动**：商户发货（order-service `POST /api/orders/merchant/{id}/ship`）携带 `carrierCode + trackingNo` → 发货成功自动回调物流服务**创建运单**（物流服务不可用不阻断发货，仅记日志）
  - **运单/轨迹**：一子订单一运单（SubOrderId 唯一）+ 运单号唯一；轨迹状态机 Created→InTransit→OutForDelivery→Signed，任意状态可转 Exception（签收记录 SignedAt）
  - **物流公司**（平台 admin）：创建/更新/启停 + 启用列表（商户发货选择）；种子 6 家公司（顺丰/圆通/中通/韵达/京东/EMS 含官网查询链接）
  - **查询**：买家按子订单查我的物流（BuyerUserId 隔离，他人 404）+ 商户运单列表/详情（status 过滤）
  - **内部接口**：`POST /internal/shipments`（创建运单）+ `POST /internal/tracks/advance`（模拟物流回调推进轨迹）
  - 数据库：MMP_Logistics 库 + 网关 `/api/logistics/**` 直通路由（8013）
- **Phase 2 Week 11：settlement-service（8014 结算）**：
  - **佣金规则**（平台 admin）：按商户设置佣金比例（0-100%，一商户一条），未配置用平台默认（DefaultCommissionRate=5）
  - **结算单生成**：按周期（可选）拉 order-service 已完成子订单 → 排除已结算 → 按商户聚合 + 佣金计算（`SettlementItem.SubOrderId` 唯一索引**幂等防重**，重复生成 skipped 计数）
  - **结算流转**：Pending → Settled（确认）→ Paid（打款），越权流转 400
  - **商户端**：结算单列表/详情/概览（待结算+已结算金额与单数）/我的佣金比例
  - 数据库：MMP_Settlement 库 + 网关 `/api/settlements/**`、`/api/commission-rules/**` 直通路由（8014）
- **order-service 配合改动**：`SubOrder` 新增 `CarrierCode/TrackingNo` 字段（发货写入）+ `Ship(carrierCode, trackingNo)`；`Complete()` 写入完成时间（`UpdatedAt`）；新增内部接口 `GET /api/orders/internal/completed-suborders`（结算数据源，X-Internal-Key）
- 新增模块文档 `docs/modules/logistics-service.md`、`docs/modules/settlement-service.md`

### Verified
- 全量编译 0 警告 0 错误（26 个项目，仅环境性 NU1900 缓存警告）
- 物流冒烟全通过：健康 → 内部建运单（公司名自动带出）→ 重复创建 400 → 轨迹推进至签收 → 签收后再推进 400 → **商户发货 → 运单自动创建** → 商户列表/详情 → 买家查自己 ✅ / 查他人 404 → 公司管理（admin）→ 买家调平台 403 → 网关转发
- 结算冒烟全通过：健康 → 佣金规则 10% → 生成结算单（62.30 → 佣金 6.23 → 结算 56.07，明细逐条正确）→ 重复生成幂等 skipped=2 → 确认结算 → 打款 → 商户列表/概览/佣金比例 → 缺商户头 400 → 网关转发

### Notes
- **Bug 修复**：充血模型下新建子实体（ShipmentTrack，客户端 Guid 主键）经导航集合添加时被 EF 推断为 Unchanged → 误判 UPDATE 0 行并发异常 → 必须显式 `db.Tracks.Add(track)` 标记 Added
- 既有行为确认：内部接口 `[FromHeader] string key`（非空引用类型）在 `[ApiController]` 下缺头自动 400（pay-internal 同样行为），与错误密钥 401 语义一致，保持系统一致性
- 结算金额口径：按子订单 `TotalAmount`（商品金额）计佣，优惠分摊/退款冲抵留待后续阶段增强

---

## [v5.8] - 2026-08-02

### Added
- **Phase 2 Week 10-11：review-service（8012 商品评价）**：
  - **买家评价**：创建评价（评分 1-5 / 内容 / 匿名，关联订单商品，同订单项防重复唯一索引）+ 我的评价（分页）
  - **商户管理**：评价列表（productId/rating/status 过滤 + 分页）+ 回复（可修改）+ 隐藏/恢复可见（违规评价下架）
  - **C 端公开**：商品评价列表（仅可见 + 平均分 + 评分分布 + 分页 + rating 过滤），匿名评价显示「匿名用户」
  - 评分统计口径 = 仅可见评价（隐藏后自动剔除，实测 3星隐藏 → 平均 5.0）
  - 数据库：MMP_Review 库（Reviews 表，唯一索引防重复评价）+ 网关 `/api/reviews/**` 直通路由（8012）
  - 多租户三重防护（商户维度）+ 买家 UserId 隔离 + 跨商户实测隔离
- 新增模块文档 `docs/modules/review-service.md`
- 新增冒烟脚本 `tests/smoke-review.sh`（20 项断言，登录优先 + 数据清理，可重复执行）

### Verified
- 全量编译 0 警告 0 错误（24 个项目，仅环境性 NU1900 缓存警告）
- 冒烟 20/20 全通过：登录 → 建评（5星）→ 重复评价拦截 → 无效评分 400 → 匿名 3 星 → 我的评价 → 缺商户头 400 → 商户列表 → 跨商户隔离 → 公开统计（平均4）→ 评分过滤 → 商户回复 → 隐藏（统计变 1 条/平均 5）→ hidden 过滤 → 恢复可见 → 网关转发 → 匿名展示

### Notes
- **Bug 修复**：公开列表 `totalCount` 曾误用全量统计计数（`all.Count`），评分过滤后计数仍为全量 → 改为过滤后的 `total`（冒烟第 11 项捕获）
- 踩坑记录：运行中的服务会锁定 bin\Release\*.dll 导致 Release 重编译 MSB3021 → 先停端口进程再编译；DTO `[Range]` 校验在 ModelState 层拦截（400），领域层错误码不触发

---

## [v5.7] - 2026-08-02

### Added
- **Phase 2 Week 10-11：promotion-service（8009 促销/优惠券/满减活动）**：
  - **优惠券**：商户创建（满减券：满 X 减 Y、限量 0=不限、限领 1-99、有效期窗口）+ 列表/详情/启停
  - **买家领券**：可领券列表（公开，启用+有效期内+未领完）+ 领券（三重校验：启用/有效期/总量 + 每人限领）+ 我的券（unused/used/expired 过滤，过期按有效期推导）
  - **满减活动**：商户创建/列表/详情/启停（Draft ⇄ Active → Ended，Ended 时间窗口惰性收尾）+ C 端进行中活动查询（公开）
  - **内部核销接口**：`POST /internal/coupons/use`（X-Internal-Key），核销幂等（重复回调 Success），返回优惠金额，供 order-service 后续接线
  - 用户券为**快照模式**（领取时复制券名/规则/有效期，模板改动不影响已领券）
  - 数据库：MMP_Promotion 库（Coupons / UserCoupons / PromotionActivities）+ 网关 `/api/promotion/**` 直通路由（8009）
  - 多租户三重防护（商户维度）+ 买家 UserId 隔离 + 跨商户实测隔离
- 新增模块文档 `docs/modules/promotion-service.md`
- 新增冒烟脚本 `tests/smoke-promotion.sh`（19 项断言，可重复执行）

### Verified
- 全量编译 0 警告 0 错误（23 个项目，仅环境性 NU1900 缓存警告）
- 冒烟 19/19 全通过：健康 → 建券 → 缺商户头 400 → 列表 → 可领（公开）→ 领券 → 限领 400 → 我的券 → 内部核销（错误密钥 401/正确成功减20/重复幂等）→ 建活动 → 启用 → 进行中（公开）→ 停用 → 进行中为空 → 网关转发 → 跨商户隔离

### Notes
- 订单联动（下单选券/支付核销）未接线：内部接口已就绪，后续阶段统一接入（仿库存 reserve/confirm 模式）
- 踩坑记录：`MultiTenantEntity.MerchantId` required 需 `[SetsRequiredMembers]`（v4.9 已记，本次复用）；`dotnet xxx.dll` 从项目根启动读不到 appsettings.json → 必须 cd 输出目录

---

## [v5.6] - 2026-08-02

### Added
- **项目上下文文件 `docs/CONTEXT.md`**：会话恢复专用（概览/服务清单/当前进度/下一步/约定/踩坑），新会话整读即恢复；每阶段交付后同步更新
- 编码规范「会话边界约定」更新：新会话恢复方式改为**第一步整读 CONTEXT.md**（替代三板斧）
- Token 消耗分析报告 `docs/reports/token-usage-analysis.md`（主因=上下文过长，阶段化会话约省 84%）
- 项目长期记忆 `.workbuddy/memory/MEMORY.md` 新建（跨会话约定 + 踩坑清单）

## [v5.5] - 2026-08-02

### Added
- **Phase 2 Week 10：cart-service（8007 购物车）**：
  - 单表 CartItem（买家 UserId 隔离，同 SKU 自动合并数量，1-999）
  - API：加购/列表（含选中合计）/改量/选中/删除/清空，全接口 JWT 鉴权
  - MMP_Cart 库 + 网关 `/api/cart/**` 直通路由
- **Phase 2 Week 10：search-service（8008 商品搜索）**：
  - 商品搜索索引（ProductId 唯一 upsert），关键词/分类/价格区间 + 分页，仅在售
  - 内部接口 upsert/remove（X-Internal-Key），product-service 创建/更新/上下架自动同步（失败不阻塞）
  - MMP_Search 库 + 网关 `/api/search/**` 直通路由
- **配套改动**：merchant-service 新增内部查询接口（查商户名）；product-service 新增 Merchant/Search 客户端（命名 HttpClient 区分，修复多客户端注册覆盖）
- 新增模块文档 `docs/modules/cart-service.md`、`docs/modules/search-service.md`

### Verified
- 全量编译 0 警告 0 错误（22 项目）
- 购物车：加购/合并（1+2=3）/改量/选中合计/买家隔离/删除/清空全通过
- 搜索：索引同步（创建/更新/上下架）/关键词/价格过滤/在售过滤/内部密钥 401 全通过
- 商户名跨服务同步（product → merchant → search）验证通过



### Added
- **Phase 1 Week 9：开发完成 C 端 Web 商城（web-customer）**：
  - 项目骨架：Vue 3.5.40 + Vite 8.2.0 + TS 5 + Element Plus 2.14.3 + Pinia + Vue Router + Axios 封装（JWT 注入/401 跳登录/错误提示）
  - 页面：首页（商品网格）/ 商品详情（SKU 选择+数量）/ 登录 / 注册（注册即登录）/ 确认订单（下单+自动支付）/ 我的订单（列表+详情+取消/支付）
  - Vite dev 代理 → YARP 网关（8000），全链路联调通过
  - **product-service 新增 C 端公开查询接口**（GET /api/products/public 列表+详情，无鉴权，仅在售）
- 新增模块文档 `docs/modules/web-customer.md`

### Verified
- 前端构建成功（Vite 8）；端到端验证：公开商品列表 → 注册/登录 → 下单（预占库存）→ 支付 → 订单 Paid → 库存扣减

---

## [v5.3] - 2026-08-02

### Added
- **订单-库存联动接线（Phase 1 收尾）**：
  - order-service 接入 stock-service 客户端（IServiceClient + X-Internal-Key 命名客户端）
  - 下单预占库存（reserve），预占失败释放已占并拒绝下单（400 STOCK_INSUFFICIENT）
  - 支付确认扣减库存（confirm）、订单取消释放预占（release），失败记录日志不阻塞
  - **多 SKU 部分失败补偿**：逐项预占，任一失败自动回滚已预占项（分布式一致性）

### Verified
- 全量编译 0 警告 0 错误（Release，20 个项目）
- 冒烟全通过：建库存 → 下单自动预占（A rsv2/B rsv1）→ 库存不足拒绝 → 支付扣减（total 98/49）→ 取消释放（rsv0）→ **部分失败补偿（A×3+B×1000 失败后 A 无残留预占）**

---

## [v5.2] - 2026-08-02

### Added
- **Phase 1 Week 8：开发完成 stock-service（库存微服务，端口 8006）**：
  - 库存模型：总库存 / 预占 / 可用（可用=总-预占，防超卖）
  - 商户管理：创建 / 列表 / 详情 / 补货 / 流水审计（X-Merchant-Id 多租户）
  - **内部接口**：预占（下单）/ 确认扣减（支付）/ 释放回滚（取消），X-Internal-Key 校验，success/error 结构
  - 数据库：MMP_Stock 库（StockItems / StockTransactions）+ 网关 `/api/stock/**` → 8006
  - 延续规范：Mediator + CQRS + 充血实体 + 全注解 + Swagger Bearer
- 新增模块文档 `docs/modules/stock-service.md`

### Verified
- 全量编译 0 警告 0 错误（Release，20 个项目）
- 冒烟全通过：创建100 → 预占30(可用70) → 预占超量保护 → 扣减20(total80) → 释放10 → 补货50(total130) → 密钥401 → 流水5条 → Swagger 8 接口

### Notes
- 订单联动（下单预占/支付扣减/取消回滚）内部接口已就绪，Phase 1 收尾统一接入
- Internal.Key 与 order-service 约定一致

---

## [v5.1] - 2026-08-02

### Added
- **Phase 1 Week 7-8：开发完成 pay-service（支付微服务，端口 8005）**：
  - 支付单状态机：Pending → Success/Failed → Refunded（同订单仅一笔待支付）
  - 模拟支付（simulate 渠道，后续可切真实渠道 Strategy 扩展点）+ 退款
  - **首次服务间同步调用落地**：支付成功经 IServiceClient 回调 order-service `/pay-internal`（X-Internal-Key 校验），订单自动变 Paid
  - order-service 新增内部支付确认端点（InternalOrdersController + MarkOrderPaidInternalCommand）
  - 数据库：MMP_Pay 库 Payments 表 + 网关 `/api/pay/**` → 8005
  - 延续规范：Mediator + CQRS + 充血实体 + 全注解 + Swagger Bearer
- 新增模块文档 `docs/modules/pay-service.md`

### Verified
- 全量编译 0 警告 0 错误（Release，19 个项目）
- 冒烟全通过：创建订单 → 创建支付单 → 模拟支付 → **订单跨服务自动 Paid** → 退款 → 重复支付保护 → Swagger 5 接口

### Notes
- 跨服务调用：命名 HttpClient 默认头携带 X-Internal-Key；回调失败不阻塞支付（日志补偿）
- Internal.Key 需与 order-service 一致（配置约定）

---

## [v5.0] - 2026-08-02

### Added
- **Phase 1 Week 6-7：开发完成 order-service（订单微服务，端口 8004）**：
  - **多商户拆单**：跨商户订单自动按商户拆为子单（Order → SubOrder×N → OrderItem 快照）
  - 订单状态机：Pending→Paid→Shipped→Completed/Cancelled；子单全部完成主单自动完成
  - 买家接口（我的订单/详情/取消/支付确认）+ 商户接口（子单列表/发货/完成，X-Merchant-Id）
  - 数据库：MMP_Order 库（Orders / SubOrders / OrderItems，商品价格快照）
  - 网关路由：`/api/order/**` → 8004
  - 延续规范：Mediator + CQRS + 充血模型（拆单逻辑内聚 Order.Create）+ 全注解 + Swagger Bearer
- 新增模块文档 `docs/modules/order-service.md`

### Verified
- 全量编译 0 警告 0 错误（Release，18 个项目）
- 冒烟全通过：跨商户拆单（2 商户 3 商品，金额 120.3）→ 支付 → 发货 → 完成 → 主单自动 Completed；取消保护；Swagger 8 接口

### Notes
- **Bug 修复**：子单完成时主单 TryComplete 误判——EF Core 关系修复（relationship fixup）用不完整子单集合判断，须 `Include(o => o.SubOrders)` 加载全部子单
- 下单支付为模拟确认，pay-service（Week 7-8）接入后替换

---

## [v4.9] - 2026-08-02

### Added
- **Phase 1 Week 5-6：开发完成 product-service（商品管理微服务，端口 8003）**：
  - 分类（商户自建，父子层级 + 排序 + 软停用）+ 商品 CRUD（含多 SKU：编码/规格/价格/库存）
  - 上下架状态机（Draft→OnSale→OffSale，上架需启用 SKU）
  - **多租户隔离落地**（首个业务级）：X-Merchant-Id 请求头 → ITenantProvider → 实体 MultiTenantEntity + DbContext HasQueryFilter + Handler 显式过滤三重防护
  - 数据库：MMP_Product 库（Categories / Products / ProductSkus）
  - 网关路由：`/api/product/**` → 8003
  - 延续规范：Mediator + CQRS + 充血实体 + 全 API 注解 + Swagger Bearer
- 新增模块文档 `docs/modules/product-service.md`

### Verified
- 全量编译 0 警告 0 错误（Release，17 个项目）
- 冒烟测试全通过：分类父子 → 商品(2SKU) → 上架 → 缺商户头400 → 跨商户隔离(空/404) → 删除保护 → Swagger

### Notes
- **多租户安全修复**：首版 Update/Delete/查询未强制商户上下文（HasQueryFilter 空商户时不拦截），已改为 Handler 显式 `Where(MerchantId)` + 商户上下文必检
- 踩坑：MultiTenantEntity.MerchantId 为 required，子类构造函数需 `[SetsRequiredMembers]`

---

## [v4.8] - 2026-08-02

### Added
- **Phase 1 Week 4-5：开发完成 merchant-service（商户入驻审核微服务，端口 8002）**：
  - 入驻申请（商户名唯一 + 一个用户仅一条未终态申请）+ 我的商户 / 详情 / 分页列表
  - 审核流程（admin 角色）：批准 / 驳回（驳回必填原因），状态机 Pending→Approved/Rejected→Disabled
  - 数据库：MMP_Merchant 库 Merchants 表（Name 唯一索引 + 多组合索引）
  - 网关路由：`/api/merchant/**` → 8002（YARP 预置路由生效）
  - 延续 identity-service 规范：Mediator + CQRS + 充血实体 + 全 API 注解 + Swagger Bearer
- **BuildingBlocks.Security 增强**：
  - `CurrentUserAccessor` 提升为公共实现（+AddCurrentUser 扩展），identity/merchant 共用（消除重复代码）
  - **修复 JWT role claim**：签发改用标准短名 `role`（原 ClaimTypes 长 URI 导致 [Authorize(Roles)] 失效），配合 MapInboundClaims=false + RoleClaimType="role"
  - 改用 FrameworkReference Microsoft.AspNetCore.App（Http.Abstractions 已并入共享框架）
- identity-service：移除本地 CurrentUserAccessor（改用公共实现），JwtBearer 补 RoleClaimType
- 新增模块文档 `docs/modules/merchant-service.md`

### Verified
- 全量编译 0 警告 0 错误（Release，16 个项目）
- 冒烟测试 12 项全通过：申请 → 我的商户 → 重复申请409 → 名称占用409 → 非admin 403 → admin 列表/详情 → 审核通过(Approved) → 重复审核400 → 驳回缺原因400 → 驳回(Rejected+原因) → Swagger

### Notes
- 踩坑记录：JwtTokenService 用 ClaimTypes.Role 长 URI 签发导致角色授权失效，改短名 role 解决；Http.Abstractions 3.0+ 并入共享框架需用 FrameworkReference

---

## [v4.7] - 2026-08-02

### Added
- **Phase 1 Week 4 启动：开发完成 identity-service（用户认证微服务，端口 8001）**：
  - 注册（邮箱唯一，注册即登录）+ 登录（密码校验）+ JWT 签发（复用 BuildingBlocks.Security）
  - 登录失败锁定：连续 5 次错误锁定 15 分钟（AuthOptions 可配置）
  - 密码安全：PBKDF2-SHA256（100k 迭代 + 随机盐 + 恒定时间比较），无第三方依赖
  - 数据库：MMP_Identity 库 Users 表（Email 唯一索引）
  - 网关路由：`/api/identity/**` → 8001（YARP 预置路由生效）
  - **首个按编码规范 Phase 1 分层落地的服务**：Mediator + CQRS（Command/Query + Handler 自动扫描注册）、User 充血实体（状态机内聚）、全部 API XML 注解、Swagger 带 Bearer 认证按钮
- **BuildingBlocks.Core 新基建**：
  - `Mediator` 实现（IMediator 默认实现，DI 路由 Handler）
  - `AddCqrsHandlers` 程序集扫描注册 CQRS 处理器
  - 引用 Microsoft.Extensions.DependencyInjection.Abstractions
- **BuildingBlocks.Security 修复**：JwtOptions.SecretKey 去掉 required（required 带默认值属反模式）
- 新增模块文档 `docs/modules/identity-service.md`

### Verified
- 全量编译 0 警告 0 错误（Release，15 个项目）
- 冒烟测试全通过：注册 → 重复注册409 → 登录 → JWT 查 me → 无 token 401 → 5 次失败锁定 → 锁定后拒登 → 网关转发

### Notes
- 踩坑记录：JWT sub/role claim 默认被 inbound 映射改名，须 `MapInboundClaims=false` 保留原始 claim；Microsoft.OpenApi 2.0 中 SecurityRequirement 用 `OpenApiSecuritySchemeReference`

---

## [v4.6] - 2026-08-02

### Added
- **三个服务 API 全量补 XML 注解**（messaging / logging / email，约 100+ 处）：
  - Controller 类 + 全部 Action 的 summary/param/returns（10 个 Controller）
  - DTO 请求/响应类全部属性注释（3 个 DTO 文件）
  - 实体构造函数 / 领域方法 / DbContext DbSet / DI 注册扩展 注释补全
- 三个服务 csproj 开启 `GenerateDocumentationFile=true` + 取消 CS1591 屏蔽（缺注释编译即警告）
- Program.cs SwaggerGen 配置 `IncludeXmlComments`，Swagger UI 展示全部注解
- **编码规范新增第八节「API 注解规范」**（docs/architecture/coding-standards.md）：
  - API 项目必须开 XML 生成且不得屏蔽 CS1591；注释覆盖范围；SwaggerGen 加载配置；0 警告 0 错误验收口径
  - Phase 1 验收标准追加「API 注解」条款；禁止事项追加「API 无注解即不合格」

### Verified
- 三个服务 Release 编译全部 0 警告 0 错误
- Swagger UI 注解展示正常（Controller/Action/参数/DTO 属性均有描述）

### Notes
- 踩坑记录：Swashbuckle 7.0.0 与 .NET 10 不兼容（TypeLoadException），升级至 10.1.7（v4.5 已记）

---

## [v4.5] - 2026-08-02

### Added
- 新增编码规范文档 `docs/architecture/coding-standards.md`（v1.0，全项目强制）：
  - 三大特性规范：封装（private set + 领域方法 + 充血模型，EmailMessage 为样板）/ 继承（Entity/MultiTenantEntity 体系）/ 多态（6 处扩展点接口）
  - 设计模式应用清单（Aggregate Root / Specification / Mediator+CQRS / UnitOfWork / Strategy / Observer / Factory / Template Method）
  - 开闭原则、高内聚低耦合、消息订阅（Pub/Sub）落地要求
  - **Phase 1 业务服务开发规范（10 条强制）**：Mediator 分层（Controller→IMediator→Handler→领域服务→Repository）、CQRS 分离、订阅收敛到网关、多租户隔离、UnitOfWork 事务、消息幂等、邮件接入方式、每服务验收标准
  - 禁止事项红线（Controller 直连仓储 / 裸 setter / 改旧代码扩展 / 散落 SaveChanges / DateTime.Now 等）

### Notes
- 文档分类约定落地：架构/规范类归 `docs/architecture/`，索引与 DOC_INDEX.md 已同步

---

## [v4.4] - 2026-08-02

### Added
- 开发完成 **email-service**（自封装邮件微服务，Phase 0 Week 3）：
  - 邮件发送（MailKit SMTP）+ DryRun 开发模式（本地无 SMTP 可模拟）
  - Razor 模板渲染（RazorLight，@Model.xxx 变量插值）+ 模板 CRUD
  - 发送状态机：Pending/Sent/Failed/DeadLetter + 后台指数退避重试（60s ×2 上限 10min）
  - API：发送/批量/状态查询/手动重试/死信 + 模板管理 + 健康检查
  - 数据库：MMP_Email 库（EmailMessage / EmailTemplate）
  - 网关路由：`/api/emails/**`、`/api/templates/**` → 8015
- **BuildingBlocks.Data ORM 切换完善**（Strategy + Factory 模式）：
  - `SqlSugarRepository<T>`（SqlSugar 实现，InsertableByObject 兼容 EF 风格实体）
  - `DapperRepository<T>`（Dapper 实现，反射生成 SQL + 存储过程扩展点）
  - `IOrmStrategy` 策略标记 + `IRepositoryFactory` 仓储工厂（按 DataOptions.DefaultOrm 自动解析）
  - `AddDataLayer` 完整注册三种 ORM 实现与 SqlSugarScope 单例
- **BuildingBlocks.Communication gRPC 完善**：
  - `GrpcServiceClient`：JSON-gRPC 模式通用客户端（path 格式 "ServiceName/MethodName"）
  - `AddServiceClient` 支持 `CommunicationProtocol.Grpc` 注册
  - 新增 Grpc.Net.Client / Grpc.Core.Api 2.80.0（匹配 Aspire 13.4.6 依赖链）
- 新增模块文档 `docs/modules/email-service.md`

### Verified
- 全量编译 0 警告 0 错误（Release，14 个项目）
- email-service 冒烟测试通过：健康检查 → 创建模板 → 模板渲染发送（Welcome Xiaoma!）→ 直接发送 → 分页查询

---

## [v4.3] - 2026-08-02

### Added
- 开发完成 **logging-service**（自封装日志管理微服务，Phase 0 Week 2）：
  - 批量接收日志（POST /api/logs/batch）+ SQL Server 持久化（MMP_Infra · Logs 表）
  - 检索：按服务/级别/关键字/时间范围分页查询
  - 统计：级别分布 / Top 服务 / 时间趋势（小时/天）/ 错误率
  - 索引设计：Timestamp / (Service,Timestamp) / (Level,Timestamp) / TraceId
  - 网关路由：`/api/logs/**`、`/api/log-stats/**` → 8011
- 完善 **BuildingBlocks.Logging** 客户端：
  - 缓冲上限保护（10,000 条，防内存溢出）
  - 上报请求 10 秒超时
- 新增模块文档 `docs/modules/logging-service.md`

### Verified
- 全量编译 0 警告 0 错误（Release）
- 冒烟测试通过：健康检查 → 批量上报 5 条 → 查询/过滤 → 级别分布 → Top 服务 → 错误率 40% → 趋势

### Notes
- 本机存在 2 个提权遗留进程（MessagingService.exe PID 4202324 及其 dotnet 宿主 PID 1496，冒烟测试遗留）锁定 Debug 输出目录，需管理员权限结束或重启电脑后恢复 Debug 构建；Release 构建不受影响

---

## [v4.2] - 2026-08-02

### Added
- 开发完成 **messaging-service**（自封装消息队列微服务，Phase 0 Week 2）：
  - Outbox 模式：消息先落库（SQL Server · MMP_Infra）再异步投递，不丢消息
  - 发布订阅：REST 发布 + 后台分发器（BackgroundService 轮询）+ HTTP 回调订阅者
  - 指数退避重试（5s ×2，上限 5 分钟）+ 超限自动转死信 + 手动重试
  - 幂等去重：Idempotency 表记录 (MessageId, ConsumerUrl)，防止重复投递
  - REST API：发布/批量/状态查询/手动重试/死信管理 + 订阅管理 + 健康检查
  - 网关路由：`/api/messages/**`、`/api/subscriptions/**`、`/api/health/**` → 8010
- 扩展 **BuildingBlocks.Messaging**：
  - `HttpMessagePublisher`：通过 HTTP 调 messaging-service 发布（Strategy — HTTP 策略）
  - `MessageConsumer<T>`：订阅者消费者基类（反序列化 + 业务处理 + 消费结果）
  - `MessageBusOptions` + `AddHttpMessageBus()/AddInMemoryMessageBus()` DI 注册
- 新增模块文档 `docs/modules/messaging-service.md`（API / 订阅者接入 / 配置 / 验证结果）

### Changed
- `Directory.Packages.props`：Microsoft.Data.SqlClient 5.2.2 → 6.1.1（EF Core 10 依赖要求）
- AspireHost 注册 messaging-service（端口 8010）
- 解决方案新增第 12 个项目 MessagingService

### Verified
- 全量编译 0 警告 0 错误
- 冒烟测试通过：健康检查 → 发布 → 订阅 → 后台投递回调 200 → 消息终态 Published

---

## [v4.1] - 2026-08-02

### Changed
- 前端技术栈全面调整为 **Vue 3 全家桶**（用户确认，替代 Next.js 15 + Blazor + MAUI）：
  - C端 Web / 商户端 Web / 平台管理后台：**Vue 3.5 + Vite 8 + TypeScript 5.x + Element Plus 2.x**（原 Next.js 15 / Blazor + MudBlazor）
  - 移动端：**.NET MAUI → uni-app**（Vue 3 语法，一套代码编译 iOS + Android）
  - 桌面端：**.NET MAUI → Electron + Vue 3**（Windows + macOS 商户工作台）
- 统一配套库：Pinia 状态管理 + Vue Router 4 + Axios 请求封装 + unplugin 按需自动导入 + ESLint/Prettier
- 新增 `apps/shared/` 前端共享层：OpenAPI 自动生成 TS 客户端 / 共享组件 / Pinia store / composables
- performance-service 看板与 BI 平台改 Vue 3 + ECharts（原 Blazor）

---

## [v4.0] - 2026-08-01

### Added
- 新增 `performance-service`：压测引擎 + 内存/CPU/GC/线程池监控 + Blazor 看板 + 压测报告生成
- 新增 `im-service`：基于 SignalR 的即时通讯，支持私聊/群聊/订单推送/离线消息
- 新增 `email-service`：MailKit SMTP 发送 + IMAP 接收 + Razor 模板渲染 + 邮件队列
- 新增多端支持：移动端（.NET MAUI iOS/Android）+ 桌面端（.NET MAUI Windows/macOS）
- 新增文档管理策略：主文档 + 模块文档 + 变更记录 + 文档索引
- 新增 `E:\MultiMerchantPlatform\` 项目根目录及完整目录结构
- 新增 `docs/reports/` 目录用于存放压测报告

### Changed
- 移除 Docker 依赖，Redis 改为 Memurai（Windows 原生 Redis）或 In-Memory Cache
- 项目路径从 `C:\Users\15123\WorkBuddy\` 工作区改为 `E:\MultiMerchantPlatform\`
- 微服务数量从 16 个增加到 21 个（新增 performance/im/email + 保留原有）
- 落地路线从 18 周调整为 22 周（5 个 Phase）
- Phase 0 基础设施阶段增加 email-service 开发
- Phase 2 增加 im-service 和移动端 MAUI 骨架
- Phase 3 增加 performance-service 和桌面端 MAUI
- Phase 4 增加 performance-service 全量压测
- `BuildingBlocks.Cache` 改为支持 In-Memory / Memurai 双模式

### Removed
- Docker 依赖
- Docker Compose 配置
- Redis Docker 镜像依赖

---

## [v3.0] - 2026-08-01

### Added
- 升级到 .NET 10（从 .NET 8）
- ORM 支持 EF Core 10 / SqlSugar / Dapper 三框架可切换
- `IDbConnectionSwitcher` 接口支持按名称切换数据库连接
- 自封装 `messaging-service`（不依赖 RabbitMQ）
- 自封装 `logging-service`（不依赖 Seq/ELK）
- `BuildingBlocks.Communication`：HTTP / gRPC 可切换
- BI 分析管理平台（Blazor + MudBlazor + ECharts，参考 .NETAdmin）
- 环境检查结果（.NET 10 / Node 24 / SQL Server 已就绪）

### Changed
- 从 C#/.NET 8 升级到 .NET 10
- 消息队列从 MassTransit+RabbitMQ 改为自封装 SQL Server 持久化
- 日志从 Seq 改为自封装 logging-service
- 服务间通信从固定 gRPC 改为 HTTP/gRPC 可配置切换

---

## [v2.0] - 2026-08-01

### Changed
- 技术栈从 Java 21 + Spring Cloud Alibaba 改为 C# 12 / .NET 8 + ASP.NET Core
- API 网关从 Spring Cloud Gateway 改为 YARP
- 消息队列从 RocketMQ 改为 MassTransit + RabbitMQ
- ORM 从 MyBatis-Plus 改为 EF Core 8
- 数据库从 MySQL 改为 SQL Server
- 后台前端从 React Admin 改为 Blazor + MudBlazor
- 业务模型从单一电商改为多商户入驻平台
- 微服务数量从 10 个增加到 16 个
- 落地路线从 12 周调整为 14 周

---

## [v1.0] - 2026-08-01

### Added
- 初始方案：Java 21 + Spring Boot 3 + Spring Cloud Alibaba
- 普通电商平台，10 个微服务
- 12 周落地路线图

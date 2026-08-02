# 数据库分库分表 + 读写分离评估报告

> **阶段**：Phase 4 Week 18（缓存策略优化 + 数据库分库分表）
> **日期**：2026-08-02 · 版本 v7.3
> **状态**：方案评估完成，落地建议见「六、结论与路线」

---

## 一、现状盘点

### 1.1 库结构

平台按微服务天然完成了**垂直拆分**：19 个服务 18 个库（MMP_Infra 由 messaging/logging/performance 共用），各业务库独立：

| 库 | 核心表 | 主键 | 增长特征 |
|---|---|---|---|
| MMP_Identity | Users | Guid | 缓慢增长 |
| MMP_Product | Products / ProductSkus / Categories | Guid | 中速（商品+SKU） |
| MMP_Order | Orders / SubOrders / OrderItems | Guid | **高速（订单+子单+明细 1:N:N）** |
| MMP_Pay | PaymentOrders / Refunds | Guid | 高速 |
| MMP_Promotion | SeckillActivities / SeckillRecords | Guid | 高速（秒杀记录） |
| MMP_Stock | StockMovements | Guid | 高速（流水） |
| MMP_Logistics | Shipments / TrackingEvents | Guid | 高速（轨迹流水） |
| MMP_Settlement | SettlementOrders | Guid | 中速 |
| MMP_Risk | RiskEvents / Decisions | Guid | 中速 |
| MMP_BI | 快照表 | Guid | 定时批量 |
| MMP_Infra | Outbox / Logs / 压测结果 | Guid/自增 | **高速（日志流水）** |

### 1.2 关键结论

- **当前无数据规模压力**：开发/演示环境，全库行数远未达单表瓶颈（SQL Server 单表亿级前均无性能风险）。
- **真正的读热点**：C 端商品详情/列表（读多写少）——已由 Week 18 缓存策略优化覆盖（Redis 热数据缓存）。
- **真正的写热点**：订单/子单/明细（1:N:N 拆单结构）、日志流水、轨迹流水——分表的主战场在**时序/流水表**。

---

## 二、方案对比（水平拆分路线）

| 方案 | 说明 | 改动量 | 适用 | 结论 |
|---|---|---|---|---|
| **A. 表分区（SQL Server Partitioned Table）** | 单表按时间/键值分区，物理分文件，逻辑仍是单表 | **零代码改动**（DDL + 维护作业） | 订单、日志、轨迹等时序表 | ⭐ **首选**（当前阶段） |
| B. 应用层分表（按商户/按时间取模） | 拆 N 张物理表，EF 路由改写 | 大（DbContext 拦截/仓储重写） | 数据量已达单表瓶颈 | 暂缓（压测确认后） |
| C. 分库（读写分离 + 水平分库） | 主从复制 / 多实例分片 | 大（连接串/迁移/一致性） | 单实例吞吐瓶颈 | 暂缓（Phase 5 部署后评估） |

> **决策原则**：当前阶段（Week 18）不引入分库分表中间件（ShardingCore/ShardingSphere 等）——无规模数据支撑、改动大、收益不明确。先用**表分区 + 缓存 + 索引优化**顶住，等 Week 19 压测拿到真实吞吐基线后再按需升级方案 B/C。

---

## 三、方案 A 落地设计：表分区（推荐）

### 3.1 适用表（按增长特征）

| 库 | 表 | 分区键 | 分区粒度 | 保留策略 |
|---|---|---|---|---|
| MMP_Order | Orders / SubOrders / OrderItems | CreatedAt | 按月 | 保留 24 个月，旧分区归档 |
| MMP_Infra | Outbox / Logs | CreatedAt | 按月 | 保留 6 个月，旧分区删除 |
| MMP_Logistics | TrackingEvents | CreatedAt | 按月 | 保留 12 个月 |
| MMP_Promotion | SeckillRecords | CreatedAt | 按月 | 保留 12 个月 |

### 3.2 核心 DDL 模板（Orders 按月分区示例）

```sql
-- ① 分区函数（按月，左边界）
CREATE PARTITION FUNCTION pf_OrderByMonth (datetime2)
AS RANGE LEFT FOR VALUES ('2026-01-01', '2026-02-01', ..., '2027-12-01');

-- ② 分区方案（映射到文件组）
CREATE PARTITION SCHEME ps_OrderByMonth
AS PARTITION pf_OrderByMonth ALL TO ([PRIMARY]);

-- ③ 建表（分区列必须是聚集索引的一部分）
CREATE TABLE dbo.Orders (
    Id uniqueidentifier NOT NULL,
    OrderNo nvarchar(32) NOT NULL,
    CreatedAt datetime2 NOT NULL,   -- 分区键
    ...
) ON ps_OrderByMonth(CreatedAt);

-- ④ 滑动窗口：每月自动加新分区 / 归档旧分区（SQL Server Agent 作业）
--    加分区：ALTER PARTITION SPLIT RANGE ('2028-01-01');
--    归档：ALTER TABLE dbo.Orders SWITCH PARTITION 1 TO dbo.Orders_202501;
```

### 3.3 对 EF Core 的影响

- **查询零改动**：EF 生成的 SQL 不带分区提示，SQL Server 自动做分区裁剪（Partition Elimination）——只要查询谓词带 `CreatedAt` 范围（本项目所有订单/日志查询都带时间维度），裁剪即生效。
- 分区列约束：必须存在于**每个唯一索引/聚集索引**中（Guid 主键 + CreatedAt 复合，或改为非聚集唯一索引）。
- 迁移：分区表对 EF 的 `EnsureCreated`/迁移透明，可先手工 DDL 建分区，EF 只做业务列管理。

---

## 四、读写分离设计（方案 C 前置评估）

### 4.1 适用判断

| 条件 | 现状 | 是否触发读写分离 |
|---|---|---|
| 读:写比例 > 5:1 | 约 10:1（C 端浏览为主） | ✅ 理论上触发 |
| 单实例 CPU/IO 饱和 | 未压测（Week 19 补） | ❓ 待压测确认 |
| 主库复制延迟容忍 | 平台可容忍秒级（非强一致场景） | ✅ 大部分场景可 |

### 4.2 设计要点（Phase 5 部署时实施）

- **复制拓扑**：SQL Server Always On 可用性组（同步提交）或事务复制（异步）→ 1 主 1 只读副本。
- **连接串分离**：`BuildingBlocks.Data` 统一抽象读写两个连接串（`ConnectionStrings:XxxDb` 主 / `ConnectionStrings:XxxDbReadOnly` 读），EF 查询用只读、写操作用主库。
- **一致性策略**：读副本允许秒级延迟 → 强一致场景（订单支付后立即查详情）强制走主库（按接口标注 `[RequirePrimary]` 或读未命中回源主库）。
- **缓存叠加**：Redis 缓存命中先于读副本 → 读副本只承接缓存穿透流量，副本压力可控。

---

## 五、缓存策略（Week 18 已落地，与分库分表协同）

| 场景 | 方案 | 失效机制 |
|---|---|---|
| C 端商品详情 | Redis `product:public:detail:{id}` TTL 5min | 更新/上下架主动 Remove |
| C 端商品列表 | Redis `product:public:list:v{ver}:{page}:{size}` TTL 30s | 写操作自增 `list:version` 整体失效 |
| C 端进行中秒杀 | Redis `seckill:active:list` TTL 10s | 活动启停主动 Remove |
| 秒杀库存 | Redis Lua 原子预扣（防超卖） | 活动启停预热/清理 |

> 缓存把读热点从 DB 前置到 Redis → 表分区/读写分离的收益窗口被推后，分库分表启动时机可以更从容。

---

## 六、结论与路线

1. **本周（Week 18）**：不做分库分表中间件。完成缓存策略优化（✅）+ 网关限流（✅）+ 服务间 Polly 弹性（✅），文档输出本报告。
2. **Week 19**：performance-service 全量压测 → 拿到各表吞吐/延迟基线 → 若订单/日志表接近单表瓶颈，启动方案 A（表分区）DDL + 维护作业。
3. **Phase 5（部署上线）**：评估 Always On 读写分离；数据量明确突破单实例上限时再评估方案 B/C（应用层分表 / 水平分库）。
4. **长期**：订单/日志/轨迹流水表建议**默认按月分区建表**（新库模板），避免历史数据堆积后一次性改造。

---

## 附录：docs/database/ 目录规划

- `database-catalog.md`：全库表清单（19 库核心表 + 索引要点）→ 已建
- `sharding-partition-templates.sql`：分区函数/方案/滑动窗口脚本模板 → 已建
- 各服务 `docs/modules/<service>.md`：表结构明细（已有）

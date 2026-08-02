# 数据库目录（全库表清单）

> **阶段**：Phase 4 Week 18 · 2026-08-02 · v7.3
> 库名前缀 `MMP_`，实例 `localhost`（SQL Server 2025，sa/123456）
> 各表字段明细见 `docs/modules/<service>.md`；本文件为跨库总览。

## 库表总览（18 库 / 20 服务）

| 库 | 服务（端口） | 核心表 | 增长特征 |
|---|---|---|---|
| MMP_Identity | identity (8001) | Users, LoginAttempts | 缓慢 |
| MMP_Merchant | merchant (8002) | Merchants, MerchantApplications | 缓慢 |
| MMP_Product | product (8003) | Products, ProductSkus, Categories | 中速 |
| MMP_Order | order (8004) | **Orders, SubOrders, OrderItems, SeckillOrderProcesseds** | **高速** |
| MMP_Pay | pay (8005) | PaymentOrders, Refunds | 高速 |
| MMP_Stock | stock (8006) | StockMovements | 高速流水 |
| MMP_Cart | cart (8007) | CartItems | 中速 |
| MMP_Search | search (8008) | ProductIndexes | 中速 |
| MMP_Promotion | promotion (8009) | Coupons, Promotions, SeckillActivities, **SeckillRecords** | 高速（秒杀） |
| MMP_Infra | messaging (8010) / logging (8011) / performance (8017) | OutboxMessages, LogEntries, Benchmarks | **高速（日志）** |
| MMP_Review | review (8012) | Reviews | 中速 |
| MMP_Logistics | logistics (8013) | Shipments, **TrackingEvents** | 高速（轨迹） |
| MMP_Settlement | settlement (8014) | SettlementRules, SettlementOrders | 中速 |
| MMP_Email | email (8015) | Emails, EmailTemplates | 中速 |
| MMP_IM | im (8016) | Conversations, Messages, ReadReceipts | 高速 |
| MMP_Risk | risk (8018) | RiskRules, RiskEvents, Decisions, Blacklists | 中速 |
| MMP_Notification | notification (8019) | Notifications, Announcements, AnnouncementReads, Templates | 中速 |
| MMP_BI | bi-admin (8020) | DailySnapshots | 定时批量 |

## 分区建议（详见 reports/db-sharding-evaluation.md）

| 库 | 表 | 分区键 | 粒度 |
|---|---|---|---|
| MMP_Order | Orders / SubOrders / OrderItems | CreatedAt | 按月 |
| MMP_Infra | OutboxMessages / LogEntries | CreatedAt | 按月 |
| MMP_Logistics | TrackingEvents | CreatedAt | 按月 |
| MMP_Promotion | SeckillRecords | CreatedAt | 按月 |

> 分表（应用层 Sharding）与分库（读写分离/水平分库）暂缓：无规模压力，先压测再决策。

-- =============================================================================
-- 分库分表方案 A：SQL Server 表分区（按月滑动窗口）脚本模板
-- 适用：Orders / SubOrders / OrderItems / OutboxMessages / LogEntries / TrackingEvents / SeckillRecords
-- 原则：分区对 EF Core 透明（谓词带 CreatedAt 即自动裁剪），零代码改动
-- 用法：按需替换表名/字段；建议先在建表前执行 ①②，表已存在则执行 ⑤⑥ 迁移
-- =============================================================================

-- -----------------------------------------------------------------------------
-- ① 分区函数（按月，RANGE LEFT：左边界值属于左侧分区）
-- -----------------------------------------------------------------------------
CREATE PARTITION FUNCTION pf_OrderByMonth (datetime2)
AS RANGE LEFT FOR VALUES (
    '2026-01-01', '2026-02-01', '2026-03-01', '2026-04-01', '2026-05-01', '2026-06-01',
    '2026-07-01', '2026-08-01', '2026-09-01', '2026-10-01', '2026-11-01', '2026-12-01',
    '2027-01-01', '2027-02-01', '2027-03-01', '2027-04-01', '2027-05-01', '2027-06-01',
    '2027-07-01', '2027-08-01', '2027-09-01', '2027-10-01', '2027-11-01', '2027-12-01'
);
GO

-- -----------------------------------------------------------------------------
-- ② 分区方案（分区映射到文件组；如需独立文件组先建后改）
-- -----------------------------------------------------------------------------
CREATE PARTITION SCHEME ps_OrderByMonth
AS PARTITION pf_OrderByMonth ALL TO ([PRIMARY]);
GO

-- -----------------------------------------------------------------------------
-- ③ 新建表（分区列必须包含在聚集索引中）
-- -----------------------------------------------------------------------------
CREATE TABLE dbo.Orders (
    Id             uniqueidentifier NOT NULL,
    OrderNo        nvarchar(32)     NOT NULL,
    BuyerUserId    uniqueidentifier NOT NULL,
    TotalAmount    decimal(18,2)    NOT NULL,
    Status         int              NOT NULL,
    Remark         nvarchar(500)    NULL,
    IsDeleted      bit              NOT NULL DEFAULT 0,
    CreatedAt      datetime2        NOT NULL,   -- 分区键
    UpdatedAt      datetime2        NULL,
    CONSTRAINT PK_Orders PRIMARY KEY CLUSTERED (CreatedAt, Id)
) ON ps_OrderByMonth(CreatedAt);
GO

-- 唯一索引注意：分区列必须参与每个唯一索引（OrderNo 唯一索引改为 (CreatedAt, OrderNo)）
CREATE UNIQUE INDEX UX_Orders_OrderNo ON dbo.Orders (CreatedAt, OrderNo);
GO

-- -----------------------------------------------------------------------------
-- ④ 查询自动裁剪验证（执行计划应只访问目标分区）
-- -----------------------------------------------------------------------------
SELECT * FROM dbo.Orders WHERE CreatedAt >= '2026-08-01' AND CreatedAt < '2026-09-01';
GO

-- -----------------------------------------------------------------------------
-- ⑤ 存量表迁移（已有非分区表 → 分区表）
--    步骤：改名 → 建分区表 → 插入迁移 → 改名 → 重建约束/索引
-- -----------------------------------------------------------------------------
-- EXEC sp_rename 'dbo.Orders', 'Orders_Old';
-- CREATE TABLE dbo.Orders (...) ON ps_OrderByMonth(CreatedAt);  -- 同 ③
-- INSERT INTO dbo.Orders (Id, OrderNo, ...) SELECT Id, OrderNo, ... FROM dbo.Orders_Old;
-- DROP TABLE dbo.Orders_Old;
GO

-- -----------------------------------------------------------------------------
-- ⑥ 滑动窗口维护（每月一次，SQL Server Agent 作业）
--    新增下月分区 + 归档最旧分区
-- -----------------------------------------------------------------------------
-- 加分区（下月）：
-- ALTER PARTITION SCHEME ps_OrderByMonth NEXT USED [PRIMARY];
-- ALTER PARTITION FUNCTION pf_OrderByMonth SPLIT RANGE ('2028-01-01');
--
-- 归档最旧分区（如 2025-12 数据 → 独立表后移出库）：
-- ALTER TABLE dbo.Orders SWITCH PARTITION 1 TO dbo.Orders_202512;
-- 备份/删除归档表后重新创建空表承接 PARTITION 1
GO

# search-service 商品搜索服务

> 模块文档 · 摩登时代 · 2026-08-02 · Phase 2 Week 10

## 一、概述

| 项 | 值 |
|---|---|
| 端口 | **8008** |
| 数据库 | `MMP_Search`（ProductSearchIndexes 表） |
| 网关路由 | `/api/search/**`（直通，无前缀剥离） |
| 公开接口 | C 端搜索无需鉴权（仅在售商品） |
| 内部接口 | X-Internal-Key 校验（product-service 同步用） |

**定位**：商品搜索专用索引服务——从 product-service 同步商品快照，C 端搜索只查本库，不加重 product-service 负担。

## 二、核心设计

### 索引模型

```
ProductSearchIndex（ProductId 唯一，upsert 语义）
 ├─ 搜索字段：Name / Description（LIKE %kw%）
 ├─ 过滤字段：Status（2=在售）/ CategoryId / PriceMin / PriceMax
 └─ 展示字段：MerchantName / CoverImage / CategoryName
```

### 同步链路（商品 → 搜索）

```
product-service（创建/更新/上下架）
    ├─ MerchantServiceClient ──► merchant-service 内部接口查商户名
    └─ SearchServiceClient ────► search-service /internal/upsert
```

- **触发点**：创建商品 / 更新商品 / 上下架，保存后同步
- **失败策略**：同步失败仅记日志，**不阻塞商品主流程**（索引最终一致，可手动触发更新）
- **在售过滤**：搜索仅返回 `Status=2`（Draft/OffSale 商品不入结果，索引同步但不可见）

### 检索实现

- 关键词：`Name` / `Description` LIKE 模糊匹配
- 过滤：分类 / 价格区间（`PriceMax >= minPrice && PriceMin <= maxPrice` 区间相交）
- 分页：默认 20 条/页，最大 100
- **升级路径**：当前 LIKE 查询开发阶段足够；生产数据量大时启用 SQL Server **Full-Text**（建 FULLTEXT CATALOG + 索引，查询改 CONTAINS，表结构不变）

## 三、API 清单

| 方法 | 路径 | 说明 | 鉴权 |
|---|---|---|---|
| GET | `/api/search/products` | 搜索（keyword/categoryId/minPrice/maxPrice/page/pageSize） | 公开 |
| POST | `/api/search/internal/upsert` | 索引 upsert（商品变更同步） | 内部密钥 |
| POST | `/api/search/internal/remove` | 索引移除（商品删除） | 内部密钥 |

## 四、配套改动（product-service / merchant-service）

- **merchant-service** 新增内部接口 `GET /api/merchants/internal/{id}`（X-Internal-Key）→ 供查商户名
- **product-service** 新增 `MerchantServiceClient`（查商户名）+ `SearchServiceClient`（同步索引），命名 HttpClient 区分（修复了多命名客户端注册覆盖问题）

## 五、联调验证（2026-08-02 实测）

```
更新已有商品 → 索引同步（商户名：摩登甄选旗舰店）✅
关键词「面包」命中 1 条 ✅ → 新建商品自动同步 ✅
Draft 状态搜索不命中 ✅ → 上架后命中 ✅
价格区间 20-25 过滤 ✅ → 内部密钥错误 401 ✅
```

## 六、已知限制与扩展

- **全文检索**：LIKE 已满足当前规模，Full-Text 为生产升级项（表结构已预留）
- **重建索引**：未提供全量重建接口（可加 `POST /internal/rebuild` 从 product-service 拉全量）
- **中文分词**：LIKE 对中文无分词；Full-Text 需配置中文分词器
- **排序**：当前按更新时间倒序；后续可加销量/价格排序（Phase 3）

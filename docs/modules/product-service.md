# product-service — 商品管理微服务

> **所属阶段**：Phase 1 Week 5-6 · **优先级**：P0 · **端口**：8003
> **更新日期**：2026-08-02

## 一、职责

商户商品管理中心：

- 商品分类（商户自建，父子层级）
- 商品 CRUD（含多 SKU：编码/规格/价格/库存）
- 商品上下架（上架要求至少一个启用 SKU）
- **多租户隔离**：MerchantId 数据隔离（实体 + 查询双重过滤）

## 二、核心设计

### 多租户隔离（首个业务级多租户服务）

```
商户请求头 X-Merchant-Id ──▶ ITenantProvider.CurrentMerchantId
        │
        ├── 实体层：Category / Product 继承 MultiTenantEntity（MerchantId 必填）
        ├── 数据层：DbContext HasQueryFilter 全局过滤
        └── 应用层：Handler 显式 Where(MerchantId) 双保险
```

- 缺 X-Merchant-Id 的写操作 → 400 MERCHANT_REQUIRED
- 跨商户访问他人资源 → 404（隔离不泄露存在性）
- 平台 admin（后续管理后台）不带头时可读全量

### 实体模型

| 实体 | 说明 | 状态 |
|------|------|------|
| `Category` | 分类（父/子，排序） | IsActive 软停用 |
| `Product` | 商品（分类关联，多 SKU） | Draft → OnSale → OffSale |
| `ProductSku` | SKU（编码唯一/规格/价格/库存） | IsActive |

### 数据库（MMP_Product 库）

| 表 | 说明 | 关键索引 |
|----|------|---------|
| `Categories` | 分类 | `(MerchantId, ParentId, Name)` 唯一；`(MerchantId, SortOrder)` |
| `Products` | 商品 | `(MerchantId, CategoryId)`；`(MerchantId, Status)` |
| `ProductSkus` | SKU | `(ProductId, SkuCode)` 唯一 |

## 三、REST API

### 分类 `/api/categories`（需登录 + X-Merchant-Id）

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/categories` | 创建分类 |
| GET | `/api/categories` | 分类列表（当前商户） |
| PUT | `/api/categories/{id}` | 更新（名称/层级/排序/停用） |
| DELETE | `/api/categories/{id}` | 删除（有子分类/商品时禁止） |

### 商品 `/api/products`（需登录 + X-Merchant-Id）

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/products` | 创建商品（含 SKU ≥ 1，状态草稿） |
| GET | `/api/products` | 分页列表（状态过滤） |
| GET | `/api/products/{id}` | 详情（含 SKU） |
| PUT | `/api/products/{id}` | 更新基本信息 |
| PUT | `/api/products/{id}/status` | 上下架（上架需启用 SKU） |

**创建商品请求体**：

```json
{
  "name": "北海道吐司",
  "categoryId": "436d1c09-...",
  "description": "奶香浓郁",
  "skus": [
    { "skuCode": "HT-500G", "spec": "500g", "price": 19.9, "stock": 100 },
    { "skuCode": "HT-1KG", "spec": "1kg", "price": 35.0, "stock": 50 }
  ]
}
```

### 网关入口（YARP）

```
/api/product/**  → product-service (8003)（前缀剥离）
```

## 四、配置说明（appsettings.json）

```json
{
  "ConnectionStrings": {
    "ProductDb": "Server=localhost;Database=MMP_Product;User Id=sa;Password=123456;TrustServerCertificate=True"
  },
  "Jwt": { "SecretKey": "与 identity-service 一致" }
}
```

## 五、项目结构

```
src/services/product-service/
├── Program.cs                        # JWT 认证 + Swagger(Bearer) + 自动迁移
├── Domain/
│   ├── Entities/Category.cs          # 分类（父/子）
│   ├── Entities/Product.cs           # 商品 + ProductSku（充血模型）
│   └── Enums/ProductStatus.cs
├── Application/
│   ├── Commands/                     # Category/Product 命令 + Handlers
│   ├── Queries/ProductQueries.cs     # 列表/详情（多租户过滤）
│   ├── ProductMapper.cs / CategoryMapper.cs
│   └── DependencyInjection.cs
├── Infrastructure/
│   ├── HttpMerchantProvider.cs       # X-Merchant-Id → ITenantProvider
│   └── Persistence/                  # ProductDbContext(HasQueryFilter) + Migrations
├── DTOs/ProductDtos.cs
├── Controllers/                      # Categories / Products / Health
└── appsettings.json
```

## 六、已验证（冒烟测试）

| 场景 | 结果 |
|------|------|
| 健康检查 | ✅ healthy |
| 创建顶级/子分类 | ✅ 层级正确 |
| 分类列表 | ✅ 2 条 |
| 创建商品（2 SKU） | ✅ 状态草稿 |
| 商品列表 | ✅ 分页返回 |
| 上架（有 SKU） | ✅ 状态在售 |
| **缺 X-Merchant-Id 写操作** | ✅ 400 MERCHANT_REQUIRED |
| **其他商户查列表/详情** | ✅ 空列表 / 404（隔离生效） |
| 删除有子分类/商品的分类 | ✅ 400 保护 |
| Swagger UI | ✅ 6 接口 + Bearer + 全注解 |

## 七、已知限制与后续扩展

- **X-Merchant-Id 传递方式**：当前由调用方请求头携带；后续可改 JWT `merchant_id` claim 或网关注入
- **SKU 更新**：新增/停用/改价接口待商户端 Web 阶段细化（当前创建时一次写入）
- **库存联动**：SKU 库存与 stock-service 联动（Phase 1 Week 8）
- **图片上传**：商品图片/封面待文件服务（Phase 2）
- **搜索索引**：商品数据同步到 search-service（Phase 1 后续）

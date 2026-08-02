# merchant-service — 商户入驻审核微服务

> **所属阶段**：Phase 1 Week 4-5 · **优先级**：P0 · **端口**：8002
> **更新日期**：2026-08-02

## 一、职责

商户入驻与审核中心（多商户平台的基础）：

- 入驻申请（商户名唯一，一个用户仅一条未终态申请）
- 审核流程（管理员批准 / 驳回，驳回必填原因）
- 商户状态机（待审核 → 已通过 / 已驳回，可禁用/启用）
- 商户查询（我的商户 / 详情 / 分页列表）
- 鉴权：申请与查询需登录；审核与列表仅 **admin 角色**

## 二、核心设计

### 分层架构（遵循编码规范 Phase 1）

```
Controller → IMediator → ICommandHandler/IQueryHandler → DbContext → SQL Server
```

- **CQRS**：入驻/审核走 Command，查询走 Query（AddCqrsHandlers 自动注册）
- **充血实体**：`Merchant` 状态机（Approve/Reject/Disable/Enable 内聚）
- **角色授权**：`[Authorize(Roles="admin")]` + JWT `role` claim（短名，MapInboundClaims=false + RoleClaimType="role"）

### 商户状态机

```
Pending ──审核通过──▶ Approved ──禁用──▶ Disabled ──启用──▶ Approved
   │
   └──审核驳回──▶ Rejected（含原因，可重新申请）
```

### 数据库（MMP_Merchant 库）

| 表 | 说明 | 关键索引 |
|----|------|---------|
| `Merchants` | 商户（名称/执照/联系人/审核状态） | `Name` 唯一；`(OwnerUserId, Status)`；`Status` |

## 三、REST API

### 商户 `/api/merchants`

| 方法 | 路径 | 鉴权 | 说明 |
|------|------|------|------|
| POST | `/api/merchants/apply` | 登录 | 提交入驻申请（状态 Pending） |
| GET | `/api/merchants/me` | 登录 | 我的商户申请状态 |
| GET | `/api/merchants` | admin | 分页列表（状态过滤） |
| GET | `/api/merchants/{id}` | admin | 商户详情 |
| POST | `/api/merchants/{id}/review` | admin | 审核（批准/驳回） |

**入驻申请请求体**：

```json
{
  "name": "摩登甄选旗舰店",
  "licenseNo": "91510100MA12345678",
  "contactName": "王老板",
  "contactPhone": "13800138000",
  "contactEmail": "shop@example.com",
  "description": "主营烘焙与零食"
}
```

**审核请求体**：

```json
{ "approved": true }                    // 通过
{ "approved": false, "reason": "..." }  // 驳回（原因必填）
```

### 健康检查 `/api/health`

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/health` | 服务存活 + 数据库连通性 |

### 网关入口（YARP）

```
/api/merchant/**  → merchant-service (8002)（前缀剥离）
```

## 四、配置说明（appsettings.json）

```json
{
  "ConnectionStrings": {
    "MerchantDb": "Server=localhost;Database=MMP_Merchant;User Id=sa;Password=123456;TrustServerCertificate=True"
  },
  "Jwt": {
    "SecretKey": "与 identity-service 一致（令牌互认，必须相同）",
    "Issuer": "MultiMerchantPlatform",
    "Audience": "MultiMerchantPlatform Clients",
    "ExpiryMinutes": 120
  }
}
```

> **重要**：JWT 密钥必须与 identity-service 完全一致，否则登录令牌校验失败。

## 五、项目结构

```
src/services/merchant-service/
├── Program.cs                        # 入口 + JWT 认证(role claim) + Swagger(Bearer) + 自动迁移
├── Domain/
│   ├── Entities/Merchant.cs          # 商户实体（状态机，充血模型）
│   └── Enums/MerchantStatus.cs
├── Application/
│   ├── Commands/                     # ApplyMerchantCommand / ReviewMerchantCommand + Handlers
│   ├── Queries/MerchantQueries.cs    # 我的商户 / 详情 / 分页列表
│   ├── MerchantMapper.cs
│   └── DependencyInjection.cs
├── Infrastructure/Persistence/       # MerchantDbContext + Migrations
├── DTOs/MerchantDtos.cs              # 申请/审核/响应（全属性注解）
├── Controllers/                      # Merchants / Health（全 Action 注解）
└── appsettings.json
```

## 六、已验证（冒烟测试）

| 场景 | 结果 |
|------|------|
| 健康检查（数据库连通） | ✅ healthy |
| 注册用户 → 登录 → 入驻申请 | ✅ 状态 Pending |
| 我的商户 /api/merchants/me | ✅ 返回申请 |
| 重复申请（同用户） | ✅ 409 DUPLICATE_APPLICATION |
| 商户名占用 | ✅ 409 NAME_EXISTS |
| 非 admin 访问列表 | ✅ 403 Forbidden |
| 管理员列表 / 详情 | ✅ 分页返回 |
| 审核通过 | ✅ 状态 Approved + approvedAt |
| 重复审核（已终态） | ✅ 400 INVALID_STATE |
| 驳回缺原因 | ✅ 400 REASON_REQUIRED |
| 驳回（带原因） | ✅ 状态 Rejected + rejectReason |
| Swagger UI | ✅ 6 接口 + Bearer + 全注解 |

## 七、已知限制与后续扩展

- **审核通知**：审核通过后应发 `MerchantApproved` 事件（messaging-service）→ email-service 通知商户，待 Phase 2 接线
- **店铺信息**：店铺装修/logo/客服设置待商户端 Web（web-merchant）阶段
- **商户账号体系**：商户员工账号（店长/店员）待后续阶段
- **资质文件**：营业执照等图片上传待文件服务（Phase 2）

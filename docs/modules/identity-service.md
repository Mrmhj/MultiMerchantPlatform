# identity-service — 用户认证微服务

> **所属阶段**：Phase 1 Week 4 · **优先级**：P0 · **端口**：8001
> **更新日期**：2026-08-02

## 一、职责

平台用户体系与认证中心：

- 用户注册（邮箱唯一，注册即登录）
- 登录认证（密码校验 + 失败锁定策略）
- JWT 签发（复用 BuildingBlocks.Security JwtTokenService）
- 当前用户信息查询（JWT Claims 解析）
- 架构规范：**首个按编码规范 Phase 1 分层落地的服务**（Mediator + CQRS + 充血模型 + 全 API 注解）

## 二、核心设计

### 分层架构（符合编码规范第七节）

```
Controller → IMediator → ICommandHandler/IQueryHandler → DbContext → SQL Server
     ↑            ↑
  (仅收参数)   (BuildingBlocks.Core.CQRS.Mediator 实现)
```

- **CQRS 分离**：注册/登录走 Command，用户查询走 Query，处理器按程序集自动扫描注册（`AddCqrsHandlers`）
- **充血实体**：`User` 状态机（Active/Disabled/Locked）+ 登录失败锁定内聚在实体方法
- **密码安全**：PBKDF2-SHA256（100k 迭代，随机盐，恒定时间比较），无第三方依赖

### 登录锁定策略

```
连续失败 5 次 → 状态置 Locked，锁定 15 分钟（可配置）
锁定期间即使密码正确也拒绝登录
```

### 数据库（MMP_Identity 库）

| 表 | 说明 | 关键索引 |
|----|------|---------|
| `Users` | 用户（邮箱/密码哈希/状态/角色/登录审计） | `Email` 唯一 |

### JWT

- Claims：`sub`(用户ID) / `unique_name`(邮箱) / `role`(角色) / `merchant_id`(预留)
- 有效期 120 分钟（可配置）
- 认证中间件 `MapInboundClaims=false` 保留原始 claim 名（重要！）

## 三、REST API

### 认证 `/api/auth`

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/auth/register` | 注册（邮箱唯一，返回 JWT） |
| POST | `/api/auth/login` | 登录（返回 JWT；5 次失败锁定 15 分钟） |

**注册请求体**：

```json
{ "email": "buyer@example.com", "password": "pass123456", "displayName": "买家" }
```

**认证响应**：

```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-08-02T04:31:46Z",
  "user": { "id": "...", "email": "...", "displayName": "...", "roles": ["customer"], "status": 1 }
}
```

### 用户 `/api/users`

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/users/me` | 当前用户信息（需 `Authorization: Bearer <token>`） |

### 健康检查 `/api/health`

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/health` | 服务存活 + 数据库连通性 |

### 网关入口（YARP）

```
/api/identity/**  → identity-service (8001)（前缀剥离）
```

## 四、配置说明（appsettings.json）

```json
{
  "ConnectionStrings": {
    "IdentityDb": "Server=localhost;Database=MMP_Identity;User Id=sa;Password=123456;TrustServerCertificate=True"
  },
  "Jwt": {
    "SecretKey": "MultiMerchantPlatform_IdentityService_SecretKey_2026_Min32Chars!",
    "Issuer": "MultiMerchantPlatform",
    "Audience": "MultiMerchantPlatform Clients",
    "ExpiryMinutes": 120
  },
  "Auth": {
    "MaxFailedLoginAttempts": 5,
    "LockoutMinutes": 15
  }
}
```

> **生产注意**：`Jwt.SecretKey` 必须用环境变量注入，禁止使用默认值。

## 五、项目结构

```
src/services/identity-service/
├── Program.cs                        # 入口 + JWT 认证 + Swagger(Bearer) + 自动迁移
├── Domain/
│   ├── PasswordHasher.cs             # PBKDF2-SHA256 密码哈希
│   ├── Entities/User.cs              # 用户实体（充血模型，锁定状态机）
│   └── Enums/UserStatus.cs
├── Application/
│   ├── Options/AuthOptions.cs        # 锁定策略配置
│   ├── Commands/                     # RegisterUserCommand / LoginCommand + Handlers
│   ├── Queries/GetCurrentUserQuery.cs
│   ├── CurrentUserAccessor.cs        # ICurrentUser 实现（JWT Claims 解析）
│   ├── UserMapper.cs                 # 实体 → DTO 映射
│   └── DependencyInjection.cs
├── Infrastructure/Persistence/       # IdentityDbContext + Migrations
├── DTOs/AuthDtos.cs                  # 注册/登录/认证/用户响应（全属性注解）
├── Controllers/                      # Auth / Users / Health（全 Action 注解）
└── appsettings.json
```

## 六、已验证（冒烟测试）

| 场景 | 结果 |
|------|------|
| 健康检查（数据库连通） | ✅ healthy |
| 注册（xiaoma@test.com） | ✅ 返回 JWT + 用户信息 |
| 重复注册 | ✅ 409 邮箱已注册 |
| 登录成功 | ✅ 返回新 JWT |
| 带 token 查 /api/users/me | ✅ 返回用户信息（小马哥/customer） |
| 无 token 访问 me | ✅ 401 |
| 连续 5 次错误密码 | ✅ 第 5 次锁定 15 分钟 |
| 锁定后正确密码 | ✅ 拒绝（ACCOUNT_LOCKED） |
| 网关 /api/identity/** 转发 | ✅ 健康检查 + 注册均通 |
| Swagger UI | ✅ 4 接口 + Bearer 认证按钮 + 全注解 |

## 七、已知限制与后续扩展

- **OAuth**：方案规划含 OAuth（第 4 周先做账号密码），后续按需接入第三方登录
- **忘记密码/重置**：`ResetPassword` 领域方法已实现，接口待 merchant-service 阶段补充（需邮件验证码）
- **用户管理**：管理端用户列表/禁用/解锁接口待 Phase 1 管理后台（web-admin）阶段补充
- **验证码登录**：邮箱验证码登录可结合 email-service 后续实现

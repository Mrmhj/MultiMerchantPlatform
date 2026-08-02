# Redis 部署指南（Windows）

> 用途：Phase 4 秒杀/缓存前置基础设施。真实 Redis + 分布式锁（SETNX/RedLock 原子预扣防超卖）。
> 对应技术决策：PROJECT_PLAN.md「Phase 4 前置技术决策 — 方案 A：开源 Redis for Windows 绿色版」。

## 部署信息（2026-08-02 落地）

| 项 | 值 |
|---|---|
| 版本 | Redis 5.0.14.1（tporadowski/redis Windows 移植版，MIT） |
| 部署目录 | `E:\redis-5.0.14\` |
| Windows 服务名 | `redis`（自启动 Automatic，NetworkService 账户） |
| 监听地址 | 0.0.0.0:6379 + [::]:6379（IPv4/IPv6 全接口，防火墙已放行 TCP 6379） |
| **访问密码** | `MMP-Redis-PUctKhVRIFB48kmfI6Ek`（requirepass，**外网暴露必须带密码**） |
| 配置入口 | `E:\redis-5.0.14\redis.windows-service.conf`（服务模式） |
| 前台配置 | `E:\redis-5.0.14\redis.windows.conf`（临时前台调试用） |
| 日志 | `server_log.txt`（服务模式）+ Windows 事件日志；前台 `redis.log` |
| 数据文件 | `dump.rdb`（RDB）+ `appendonly.aof`（AOF）于部署目录 |

## 连接信息（2026-08-02 更新）

| 场景 | 地址 | 说明 |
|---|---|---|
| 本机/服务 | `localhost:6379` | 同机服务直连（仍需密码） |
| 局域网 | `192.168.1.4:6379` | 同网段设备（WiFi/交换机）访问 |
| 公网 IPv6 | `[2409:8a62:3d7:8320:7d65:1984:7a39:e97d]:6379` | 外网 IPv6 设备可直接访问（**运营商动态分配，会变化**） |
| 公网 IPv4 | `36.170.45.77:6379` ❌ | **运营商大内网 NAT（成都移动）共享地址，外部无法直连**；需要公网 IP 或内网穿透（frp/ngrok）方可 |

> ⚠️ 本机公网 IPv4 为运营商 CGNAT 共享地址，**外网 IPv4 直连不可达**；公网 IPv6 可用但动态变化。
> 如需固定公网入口，后续方案：① 向运营商申请公网 IPv4 + 路由器端口映射；② 内网穿透（frp/ngrok/cpolar 等）。

## 关键配置

```conf
port 6379
bind 0.0.0.0 ::       # 全接口监听（IPv4 + IPv6，配合密码对外暴露）
protected-mode yes    # 已配 requirepass，不触发保护限制
requirepass MMP-Redis-PUctKhVRIFB48kmfI6Ek
appendonly yes          # AOF 持久化（秒杀预扣/锁数据不丢）
maxmemory 512mb         # 内存上限（防御性）
maxmemory-policy noeviction  # 禁止淘汰（宁可写失败不丢锁/库存数据）
logfile "server_log.txt"
```

> ⚠️ 注意：`E:\redis-6.2.6` 是 Redis 6.2.6 **Linux 源码包**（无 Windows 可执行文件），不可直接运行，保留备查。
> ⚠️ 安全：Redis 密码暴露公网有爆破风险（官方称可每秒尝试 15 万次），当前密码为 20 位随机强密码；
> 生产环境建议同时启用 IP 白名单（防火墙限定来源 IP）或仅内网 + VPN。

## 常用命令

```powershell
# 服务管理（注册/启动/停止/卸载）
cd E:\redis-5.0.14
redis-server.exe --service-install redis.windows-service.conf --service-name Redis
redis-server.exe --service-start   --service-name Redis
redis-server.exe --service-stop    --service-name Redis
redis-server.exe --service-uninstall --service-name Redis

# 或系统命令
Start-Service redis / Stop-Service redis / sc.exe qc redis

# 连通与操作（服务运行中，带密码）
redis-cli.exe -a MMP-Redis-PUctKhVRIFB48kmfI6Ek ping   # → PONG
redis-cli.exe -a MMP-Redis-PUctKhVRIFB48kmfI6Ek CONFIG GET *  # 查看生效配置

# 防火墙（已创建规则 MMP-Redis-6379，重装系统后需重新执行）
netsh advfirewall firewall add rule name="MMP-Redis-6379" dir=in action=allow protocol=TCP localport=6379 profile=any
```

> ⚠️ 该版本服务注册**不支持 `--service-port` 参数**（报 unknown argument），端口以配置文件为准。

## 应用连接约定

- BuildingBlocks.Cache 连接串格式：`host:port,password=xxx`（`AddCacheService(useRedis: true, "localhost:6379,password=MMP-Redis-PUctKhVRIFB48kmfI6Ek")`；v7.1 起支持完整连接串解析，无密码的 `localhost:6379` 亦兼容）
- 不可用时自动降级 In-Memory（方案 B 兜底）
- im-service 集群扩展预留：Phase 4 将 UserConnectionManager 替换为 Redis 分布式存储

## 开机自启说明

服务启动类型 Automatic + NetworkService 账户，Windows 开机自动拉起，无需手动干预。

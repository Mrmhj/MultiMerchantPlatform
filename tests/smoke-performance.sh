#!/bin/bash
# performance-service 冒烟测试脚本（v6.3）
# 依赖：identity 8001 / performance 8017 已启动
# 覆盖：健康检查 / 鉴权拦截 / 内部指标端点 / 压测任务 CRUD / 参数校验 / 启动压测 / 停止压测 /
#       HTML 报告生成与下载 / 监控采集 / 告警生成与关闭
set -u
BASE="http://localhost:8017"
BASE_ID="http://localhost:8001"
STAMP=$(date +%Y%m%d%H%M%S)
ADMIN_EMAIL="perf_admin_${STAMP}@test.com"
PASS="Smoke@123456"
PASSED=0; FAILED=0

# sleep 兜底（沙箱 PATH 可能不含 GNU sleep）
sleep() { node -e "setTimeout(()=>{}, Number(process.argv[1])*1000)" "$1" 2>/dev/null || ping -n $(( $1 + 1 )) 127.0.0.1 > /dev/null 2>&1; }

check() { # $1=名称 $2=期望子串 $3=实际输出 $4=可选 -F 固定匹配
  if echo "$3" | grep -q ${4:-} "$2"; then echo "✅ $1"; PASSED=$((PASSED+1));
  else echo "❌ $1 | 期望含: $2 | 实际: $(echo "$3" | head -c 300)"; FAILED=$((FAILED+1)); fi
}

echo "===== 1. 前置：注册 admin（SQL 提权）====="
curl -s -m 8 -X POST $BASE_ID/api/auth/register -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$PASS\",\"displayName\":\"压测冒烟管理员\"}" > /dev/null
# 提权 SQL 须与 sqlcmd 同目录（cd + 相对文件名），避免 Git Bash 路径转义问题
cd "$(dirname "$0")"
echo "UPDATE Users SET RolesJson = '[\"admin\"]' WHERE Email = '$ADMIN_EMAIL'" > role_tmp.sql
sqlcmd -S localhost -U sa -P 123456 -d MMP_Identity -i role_tmp.sql -W > /dev/null 2>&1 || true
rm -f role_tmp.sql
ADMIN_TOKEN=$(curl -s -m 8 -X POST $BASE_ID/api/auth/login -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$PASS\"}" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
check "admin 登录（提权后）" '"token"' "\"token\":\"$ADMIN_TOKEN\""

echo "===== 2. 健康检查 ====="
check "performance 健康检查" "healthy" "$(curl -s -m 5 $BASE/api/health)"

echo "===== 3. 鉴权拦截 ====="
check "无 token 任务列表 → 401" "401" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" $BASE/api/performance/load-tests)"
check "错误内部密钥 → 401" "无效的内部调用密钥" "$(curl -s -m 5 $BASE/api/metrics -H "X-Internal-Key: WRONG")"

echo "===== 4. 内部指标端点（正确密钥）====="
METRICS=$(curl -s -m 5 $BASE/api/metrics -H "X-Internal-Key: MMP-Internal-Key-2026")
check "内部指标返回服务名" "performance-service" "$METRICS"
check "内部指标含托管内存" "managedMemoryMb" "$METRICS"
check "内部指标含 CPU" "cpuPercent" "$METRICS"

echo "===== 5. 创建压测任务（admin）====="
TASK=$(curl -s -m 5 -X POST $BASE/api/performance/load-tests \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d "{\"name\":\"冒烟-健康检查压测\",\"targetUrl\":\"http://localhost:8017/api/health\",\"httpMethod\":\"GET\",\"concurrency\":10,\"durationSeconds\":5}")
check "创建任务成功" "冒烟-健康检查压测" "$TASK"
TASK_ID=$(echo "$TASK" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
check "任务默认启用" '"enabled":true' "$TASK"

echo "===== 6. 参数校验 ====="
check "非法 URL → 400" "合法" "$(curl -s -m 5 -X POST $BASE/api/performance/load-tests \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d "{\"name\":\"坏任务\",\"targetUrl\":\"ftp://bad\",\"httpMethod\":\"GET\",\"concurrency\":1,\"durationSeconds\":1}")"
check "非法并发 → 400" "并发数" "$(curl -s -m 5 -X POST $BASE/api/performance/load-tests \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d "{\"name\":\"坏任务\",\"targetUrl\":\"http://localhost:8017/api/health\",\"httpMethod\":\"GET\",\"concurrency\":0,\"durationSeconds\":1}")"

echo "===== 7. 启动压测（并发 10 × 5s）====="
RUN=$(curl -s -m 5 -X POST $BASE/api/performance/load-tests/$TASK_ID/run \
  -H "Authorization: Bearer $ADMIN_TOKEN")
check "启动返回 Queued" '"status":"Queued"' "$RUN"
RUN_ID=$(echo "$RUN" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)

echo "===== 8. 等待压测完成并校验统计 ====="
STATUS=""
for i in 1 2 3 4 5 6 7 8 9 10; do
  RUN_DETAIL=$(curl -s -m 5 $BASE/api/performance/load-tests/runs/$RUN_ID -H "Authorization: Bearer $ADMIN_TOKEN")
  STATUS=$(echo "$RUN_DETAIL" | grep -o '"status":"[^"]*"' | head -1 | cut -d'"' -f4)
  [ "$STATUS" = "Completed" ] && break
  curl -s -m 3 $BASE/api/performance/metrics/collect -X POST -H "Authorization: Bearer $ADMIN_TOKEN" > /dev/null 2>&1 || true
  sleep 1
done
check "压测状态 Completed" "Completed" "$RUN_DETAIL"
check "总请求数 > 0" '"totalRequests":' "$RUN_DETAIL"
check "含 QPS 统计" '"qps":' "$RUN_DETAIL"
check "含 P99 统计" '"p99Ms":' "$RUN_DETAIL"
check "错误率 0" '"errorRatePercent":0' "$RUN_DETAIL"

echo "===== 9. HTML 报告生成与下载 ====="
REPORT=$(curl -s -m 5 -o /dev/null -w "%{http_code}" $BASE/api/performance/load-tests/runs/$RUN_ID/report -H "Authorization: Bearer $ADMIN_TOKEN")
check "报告下载 → 200" "200" "$REPORT"
REPORT_BODY=$(curl -s -m 5 $BASE/api/performance/load-tests/runs/$RUN_ID/report -H "Authorization: Bearer $ADMIN_TOKEN" | head -c 200)
check "报告含 HTML 头" "<!DOCTYPE html" "$REPORT_BODY"
REPORT_PATH=$(echo "$RUN_DETAIL" | grep -o '"reportPath":"[^"]*"' | head -1 | cut -d'"' -f4)
check "报告路径已记录" "loadtest-" "$REPORT_PATH"
[ -f "E:/MultiMerchantPlatform/docs/reports/$REPORT_PATH" ] && { echo "✅ 报告文件已写入 docs/reports"; PASSED=$((PASSED+1)); } || { echo "❌ 报告文件缺失: $REPORT_PATH"; FAILED=$((FAILED+1)); }

echo "===== 10. 停止压测（长任务）====="
LONG_TASK=$(curl -s -m 5 -X POST $BASE/api/performance/load-tests \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d "{\"name\":\"冒烟-长任务\",\"targetUrl\":\"http://localhost:8017/api/health\",\"httpMethod\":\"GET\",\"concurrency\":5,\"durationSeconds\":60}")
LONG_TASK_ID=$(echo "$LONG_TASK" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
LONG_RUN=$(curl -s -m 5 -X POST $BASE/api/performance/load-tests/$LONG_TASK_ID/run -H "Authorization: Bearer $ADMIN_TOKEN")
LONG_RUN_ID=$(echo "$LONG_RUN" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
STOP_CODE=$(curl -s -m 5 -o /dev/null -w "%{http_code}" -X POST $BASE/api/performance/load-tests/runs/$LONG_RUN_ID/stop -H "Authorization: Bearer $ADMIN_TOKEN")
check "停止请求成功" "200" "$STOP_CODE"
sleep 2
STOPPED=$(curl -s -m 5 $BASE/api/performance/load-tests/runs/$LONG_RUN_ID -H "Authorization: Bearer $ADMIN_TOKEN")
check "长任务已取消" '"status":"Cancelled"' "$STOPPED"

echo "===== 11. 监控采集（连续 3 轮触发宕机告警）====="
for i in 1 2 3; do
  curl -s -m 10 -X POST $BASE/api/performance/metrics/collect -H "Authorization: Bearer $ADMIN_TOKEN" > /dev/null
done
LATEST=$(curl -s -m 5 "$BASE/api/performance/metrics/latest" -H "Authorization: Bearer $ADMIN_TOKEN")
check "最新快照含 performance-service" "performance-service" "$LATEST"
check "最新快照含 identity-service" "identity-service" "$LATEST"
check "最新快照含 order-service（宕机）" '"isUp":false' "$LATEST"
SERVICES=$(curl -s -m 5 "$BASE/api/performance/metrics/services" -H "Authorization: Bearer $ADMIN_TOKEN")
check "已监控服务列表" "performance-service" "$SERVICES"
ALERTS=$(curl -s -m 5 "$BASE/api/performance/alerts?status=Open&pageSize=50" -H "Authorization: Bearer $ADMIN_TOKEN")
check "存在 ServiceDown 告警" "ServiceDown" "$ALERTS"
check "告警含 order-service" "order-service" "$ALERTS"

echo "===== 12. 告警查询与手动关闭 ====="
ALERT_ID=$(echo "$ALERTS" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
RESOLVED=$(curl -s -m 5 -X PUT $BASE/api/performance/alerts/$ALERT_ID/resolve -H "Authorization: Bearer $ADMIN_TOKEN")
check "手动关闭告警" '"status":"Resolved"' "$RESOLVED"

echo "===== 13. 运行历史查询 ====="
RUNS=$(curl -s -m 5 "$BASE/api/performance/load-tests/runs?taskId=$TASK_ID" -H "Authorization: Bearer $ADMIN_TOKEN")
check "运行历史含 1 条" '"totalCount":1' "$RUNS"

echo ""
echo "========== 冒烟测试结果：通过 $PASSED / 失败 $FAILED =========="
exit $((FAILED > 0 ? 1 : 0))

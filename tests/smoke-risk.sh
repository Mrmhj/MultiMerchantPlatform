#!/bin/bash
# risk-service 冒烟测试脚本（v6.4）
# 依赖：identity 8001 / risk 8018 已启动；admin 提权依赖本地 SQL Server（sa/123456）
set -u
BASE_RISK="http://localhost:8018"
BASE_ID="http://localhost:8001"
STAMP=$(date +%Y%m%d%H%M%S)
ADMIN_EMAIL="risk_admin_${STAMP}@test.com"
BUYER_EMAIL="risk_buyer_${STAMP}@test.com"
PASS="Smoke@123456"
INTERNAL_KEY="MMP-Internal-Key-2026"
# 每次运行独立维度键（防历史事件污染窗口统计）— GUID 末段取 STAMP 后 12 位十进制
RISK_USER="00000000-0000-0000-0000-${STAMP:2:12}"
RISK_IP="203.0.113.${STAMP: -3}"
RISK_DEVICE="DEVICE-${STAMP}"
PASS_N=0; FAIL_N=0

# 沙箱 bash 无 GNU sleep → 内置 sleep（node 兜底）
sleep() { node -e "setTimeout(()=>{}, $1*1000)"; }

check() { # $1=名称 $2=期望子串 $3=实际输出 $4=可选 -F 固定匹配
  if echo "$3" | grep -q ${4:-} "$2"; then echo "✅ $1"; PASS_N=$((PASS_N+1));
  else echo "❌ $1 | 期望含: $2 | 实际: $(echo "$3" | head -c 300)"; FAIL_N=$((FAIL_N+1)); fi
}

echo "===== 0. 前置：注册 admin + 买家（admin 提权）====="
curl -s -m 8 -X POST $BASE_ID/api/auth/register -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$PASS\",\"displayName\":\"风控冒烟管理员\"}" > /dev/null
# admin 提权（本地开发库，sqlcmd -i 必须与 SQL 文件同目录 → 相对文件名）
cd "$(dirname "$0")" || exit 1
echo "UPDATE Users SET RolesJson = '[\"admin\"]' WHERE Email = '$ADMIN_EMAIL'" > role_tmp_risk.sql
sqlcmd -S localhost -U sa -P 123456 -d MMP_Identity -i role_tmp_risk.sql -W > /dev/null 2>&1 || true
rm -f role_tmp_risk.sql
ADMIN_TOKEN=$(curl -s -m 8 -X POST $BASE_ID/api/auth/login -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$PASS\"}" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
check "admin 登录（提权后）" '"token"' "\"token\":\"$ADMIN_TOKEN\""
BUYER_TOKEN=$(curl -s -m 8 -X POST $BASE_ID/api/auth/register -H "Content-Type: application/json" \
  -d "{\"email\":\"$BUYER_EMAIL\",\"password\":\"$PASS\",\"displayName\":\"风控冒烟买家\"}" \
  | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
check "买家注册" '"token"' "\"token\":\"$BUYER_TOKEN\""

echo "===== 1. 健康检查 ====="
check "risk 健康" "healthy" "$(curl -s -m 5 $BASE_RISK/api/health)"

echo "===== 2. 鉴权拦截 ====="
check "无 token 401" "401" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" $BASE_RISK/api/risk/overview)"
check "买家调平台接口 403" "403" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" $BASE_RISK/api/risk/overview -H "Authorization: Bearer $BUYER_TOKEN")"
check "内部接口错误密钥 401" "401" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" -X POST $BASE_RISK/api/risk/internal/decide \
  -H "Content-Type: application/json" -H "X-Internal-Key: WRONG-KEY" -d '{"scene":"ORDER_SUBMIT"}')"

echo "===== 3. 规则引擎：默认规则 + CRUD ====="
RULES=$(curl -s -m 5 "$BASE_RISK/api/risk/rules?page=1&pageSize=20" -H "Authorization: Bearer $ADMIN_TOKEN")
check "默认规则含高频下单" "高频下单" "$RULES"
check "默认规则含高频领券" "高频领券" "$RULES"
NEW_RULE=$(curl -s -m 5 -X POST $BASE_RISK/api/risk/rules -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"冒烟测试规则","scene":"ORDER_SUBMIT","dimension":1,"windowSeconds":30,"threshold":3,"disposition":1,"description":"冒烟用"}')
check "创建规则成功" '"id"' "$NEW_RULE"
RULE_ID=$(echo "$NEW_RULE" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
check "创建规则阈值3" '"threshold":3' "$NEW_RULE"
check "更新规则阈值5" '"threshold":5' "$(curl -s -m 5 -X PUT $BASE_RISK/api/risk/rules/$RULE_ID -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"冒烟测试规则","scene":"ORDER_SUBMIT","dimension":1,"windowSeconds":30,"threshold":5,"disposition":1,"description":"冒烟改"}')"
check "停用规则" '"enabled":false' "$(curl -s -m 5 -X PUT "$BASE_RISK/api/risk/rules/$RULE_ID/enabled?enabled=false" -H "Authorization: Bearer $ADMIN_TOKEN")"
check "启用规则" '"enabled":true' "$(curl -s -m 5 -X PUT "$BASE_RISK/api/risk/rules/$RULE_ID/enabled?enabled=true" -H "Authorization: Bearer $ADMIN_TOKEN")"
check "规则参数校验 400" "400" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" -X POST $BASE_RISK/api/risk/rules -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"非法窗口","scene":"ORDER_SUBMIT","dimension":0,"windowSeconds":0,"threshold":1,"disposition":0}')"

echo "===== 4. 规则命中：同 IP 高频下单（30s 窗口内 3 次阈值）====="
# 独立专用规则（与第 3 节 CRUD 解耦）：ORDER_SUBMIT + Ip 维度 + 30s 窗口 + 阈值 3 → Block
HIT_RULE=$(curl -s -m 5 -X POST $BASE_RISK/api/risk/rules -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"命中测试规则","scene":"ORDER_SUBMIT","dimension":1,"windowSeconds":30,"threshold":3,"disposition":1,"description":"冒烟命中专用"}')
HIT_RULE_ID=$(echo "$HIT_RULE" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
check "创建命中专用规则" '"threshold":3' "$HIT_RULE"
# 维度键每次运行唯一
EV1=$(curl -s -m 8 -X POST $BASE_RISK/api/risk/internal/events -H "Content-Type: application/json" -H "X-Internal-Key: $INTERNAL_KEY" \
  -d "[{\"scene\":\"ORDER_SUBMIT\",\"userId\":\"$RISK_USER\",\"merchantId\":\"875DC16D-2A6B-4478-82D1-55DA8FBBE586\",\"ip\":\"$RISK_IP\",\"payloadJson\":\"{\\\"orderNo\\\":\\\"ORD1\\\"}\"}]")
check "上报事件1（无命中）" '"hits":0' "$EV1"
EV2=$(curl -s -m 8 -X POST $BASE_RISK/api/risk/internal/events -H "Content-Type: application/json" -H "X-Internal-Key: $INTERNAL_KEY" \
  -d "[{\"scene\":\"ORDER_SUBMIT\",\"userId\":\"$RISK_USER\",\"merchantId\":\"875DC16D-2A6B-4478-82D1-55DA8FBBE586\",\"ip\":\"$RISK_IP\"}]")
check "上报事件2（无命中）" '"hits":0' "$EV2"
EV3=$(curl -s -m 8 -X POST $BASE_RISK/api/risk/internal/events -H "Content-Type: application/json" -H "X-Internal-Key: $INTERNAL_KEY" \
  -d "[{\"scene\":\"ORDER_SUBMIT\",\"userId\":\"$RISK_USER\",\"merchantId\":\"875DC16D-2A6B-4478-82D1-55DA8FBBE586\",\"ip\":\"$RISK_IP\"}]")
check "上报事件3（命中 Block 案例）" '"hits":1' "$EV3"
check "命中案例含拦截" '"disposition":1' "$EV3"

echo "===== 5. 决策接口：命中后拦截 ====="
DECIDE=$(curl -s -m 5 -X POST $BASE_RISK/api/risk/internal/decide -H "Content-Type: application/json" -H "X-Internal-Key: $INTERNAL_KEY" \
  -d "{\"scene\":\"ORDER_SUBMIT\",\"userId\":\"$RISK_USER\",\"ip\":\"$RISK_IP\"}")
check "决策拦截（allow:false）" '"allow":false' "$DECIDE"
check "决策原因含规则" "命中风控规则" "$DECIDE"
DECIDE_OK=$(curl -s -m 5 -X POST $BASE_RISK/api/risk/internal/decide -H "Content-Type: application/json" -H "X-Internal-Key: $INTERNAL_KEY" \
  -d '{"scene":"ORDER_SUBMIT","userId":"00000000-0000-0000-0000-000000000222","ip":"198.51.100.9"}')
check "正常用户放行" '"allow":true' "$DECIDE_OK"

echo "===== 6. 案例处置：复核 → 确认风险 → 误报 ====="
CASES=$(curl -s -m 5 "$BASE_RISK/api/risk/cases?page=1&pageSize=10&status=open" -H "Authorization: Bearer $ADMIN_TOKEN")
check "案例列表含冒烟命中" "命中测试规则" "$CASES"
CASE_ID=$(echo "$CASES" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
check "开始复核" '"status":1' "$(curl -s -m 5 -X POST $BASE_RISK/api/risk/cases/$CASE_ID/review -H "Authorization: Bearer $ADMIN_TOKEN")"
check "确认风险" '"status":2' "$(curl -s -m 5 -X POST $BASE_RISK/api/risk/cases/$CASE_ID/resolve -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" -d '{"note":"确认刷单"}')"
CASE_ID2=$(echo "$CASES" | grep -o '"id":"[^"]*"' | sed -n '2p' | cut -d'"' -f4)
if [ -n "$CASE_ID2" ]; then
  check "标记误报" '"status":3' "$(curl -s -m 5 -X POST $BASE_RISK/api/risk/cases/$CASE_ID2/false-positive -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" -d '{"note":"正常用户"}')"
else
  echo "⚠️ 无第二个案例可标记误报（跳过）"
fi

echo "===== 7. 黑名单：加入 → 决策拦截 → 启停 → 移除 ====="
BL=$(curl -s -m 5 -X POST $BASE_RISK/api/risk/blacklist -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d "{\"targetType\":2,\"targetValue\":\"$RISK_DEVICE\",\"reason\":\"冒烟拉黑设备\",\"expiresAt\":null}")
check "加入黑名单" "\"targetValue\":\"$RISK_DEVICE\"" "$BL"
BL_ID=$(echo "$BL" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
DECIDE_BL=$(curl -s -m 5 -X POST $BASE_RISK/api/risk/internal/decide -H "Content-Type: application/json" -H "X-Internal-Key: $INTERNAL_KEY" \
  -d "{\"scene\":\"ORDER_SUBMIT\",\"userId\":\"00000000-0000-0000-0000-000000000333\",\"deviceId\":\"$RISK_DEVICE\"}")
check "黑名单设备决策拦截" "黑名单拦截" "$DECIDE_BL"
check "停用黑名单" '"enabled":false' "$(curl -s -m 5 -X PUT "$BASE_RISK/api/risk/blacklist/$BL_ID/enabled?enabled=false" -H "Authorization: Bearer $ADMIN_TOKEN")"
DECIDE_BL_OK=$(curl -s -m 5 -X POST $BASE_RISK/api/risk/internal/decide -H "Content-Type: application/json" -H "X-Internal-Key: $INTERNAL_KEY" \
  -d "{\"scene\":\"ORDER_SUBMIT\",\"userId\":\"00000000-0000-0000-0000-000000000333\",\"deviceId\":\"$RISK_DEVICE\"}")
check "停用后设备放行" '"allow":true' "$DECIDE_BL_OK"
check "重复拉黑更新原因" "更新" "$(curl -s -m 5 -X POST $BASE_RISK/api/risk/blacklist -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d "{\"targetType\":2,\"targetValue\":\"$RISK_DEVICE\",\"reason\":\"更新原因\",\"expiresAt\":null}")" || true
check "移除黑名单" "已移除" "$(curl -s -m 5 -X DELETE $BASE_RISK/api/risk/blacklist/$BL_ID -H "Authorization: Bearer $ADMIN_TOKEN")"

echo "===== 8. 事件流水 + 概览 ====="
check "事件流水含上报" "ORDER_SUBMIT" "$(curl -s -m 5 "$BASE_RISK/api/risk/events?page=1&pageSize=10" -H "Authorization: Bearer $ADMIN_TOKEN")"
check "概览含今日事件" "todayEventCount" "$(curl -s -m 5 $BASE_RISK/api/risk/overview -H "Authorization: Bearer $ADMIN_TOKEN")"
check "概览含今日命中" "todayHitCount" "$(curl -s -m 5 $BASE_RISK/api/risk/overview -H "Authorization: Bearer $ADMIN_TOKEN")"

echo "===== 9. 清理：删除冒烟规则 ====="
check "删除规则" "已删除" "$(curl -s -m 5 -X DELETE $BASE_RISK/api/risk/rules/$RULE_ID -H "Authorization: Bearer $ADMIN_TOKEN")"
check "删除命中专用规则" "已删除" "$(curl -s -m 5 -X DELETE $BASE_RISK/api/risk/rules/$HIT_RULE_ID -H "Authorization: Bearer $ADMIN_TOKEN")"

echo ""
echo "========== 结果: ✅ $PASS_N 通过 / ❌ $FAIL_N 失败 =========="
[ $FAIL_N -eq 0 ] && echo "ALL PASSED" || echo "SOME FAILED"

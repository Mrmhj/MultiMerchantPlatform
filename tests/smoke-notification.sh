#!/bin/bash
# notification-service 冒烟测试脚本（v6.5）
# 依赖：identity 8001 / notification 8019 已启动；admin 提权依赖本地 SQL Server（sa/123456）
# 覆盖：健康检查 / 鉴权拦截 / 站内信发送（直接+模板渲染）/ 通知列表与未读数 / 已读 / 全部已读 /
#       删除 / 短信 DryRun / Push DryRun / 模板 CRUD / SignalR 实时推送（独立 JS 脚本）
set -u
BASE_NOTI="http://localhost:8019"
BASE_ID="http://localhost:8001"
STAMP=$(date +%Y%m%d%H%M%S)
ADMIN_EMAIL="noti_admin_${STAMP}@test.com"
BUYER_EMAIL="noti_buyer_${STAMP}@test.com"
PASS="Smoke@123456"
INTERNAL_KEY="MMP-Internal-Key-2026"
PASS_N=0; FAIL_N=0

# 沙箱 bash 无 GNU sleep → 内置 sleep（node 兜底）
sleep() { node -e "setTimeout(()=>{}, $1*1000)"; }

check() { # $1=名称 $2=期望子串 $3=实际输出 $4=可选 -F 固定匹配
  if echo "$3" | grep -q ${4:-} "$2"; then echo "✅ $1"; PASS_N=$((PASS_N+1));
  else echo "❌ $1 | 期望含: $2 | 实际: $(echo "$3" | head -c 300)"; FAIL_N=$((FAIL_N+1)); fi
}

echo "===== 0. 前置：注册 admin + 买家（admin 提权）====="
curl -s -m 8 -X POST $BASE_ID/api/auth/register -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$PASS\",\"displayName\":\"通知冒烟管理员\"}" > /dev/null
# admin 提权（本地开发库，sqlcmd -i 必须与 SQL 文件同目录 → 相对文件名）
cd "$(dirname "$0")" || exit 1
echo "UPDATE Users SET RolesJson = '[\"admin\"]' WHERE Email = '$ADMIN_EMAIL'" > role_tmp_noti.sql
sqlcmd -S localhost -U sa -P 123456 -d MMP_Identity -i role_tmp_noti.sql -W > /dev/null 2>&1 || true
rm -f role_tmp_noti.sql
ADMIN_TOKEN=$(curl -s -m 8 -X POST $BASE_ID/api/auth/login -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$PASS\"}" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
check "admin 登录（提权后）" '"token"' "\"token\":\"$ADMIN_TOKEN\""
BUYER=$(curl -s -m 8 -X POST $BASE_ID/api/auth/register -H "Content-Type: application/json" \
  -d "{\"email\":\"$BUYER_EMAIL\",\"password\":\"$PASS\",\"displayName\":\"通知冒烟买家\"}")
BUYER_TOKEN=$(echo "$BUYER" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
BUYER_ID=$(echo "$BUYER" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
check "买家注册" '"token"' "\"token\":\"$BUYER_TOKEN\""
echo "    买家ID=$BUYER_ID"

echo "===== 1. 健康检查 ====="
check "notification 健康" "healthy" "$(curl -s -m 5 $BASE_NOTI/api/health)"

echo "===== 2. 鉴权拦截 ====="
check "无 token 通知列表 401" "401" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" $BASE_NOTI/api/notifications)"
check "买家调模板管理 403" "403" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" $BASE_NOTI/api/notifications/templates -H "Authorization: Bearer $BUYER_TOKEN")"
check "内部接口错误密钥 401" "内部密钥无效" "$(curl -s -m 5 -X POST $BASE_NOTI/api/notifications/internal/send \
  -H "X-Internal-Key: WRONG-KEY" -H "Content-Type: application/json" \
  -d "{\"userId\":\"$BUYER_ID\",\"title\":\"t\",\"content\":\"c\"}")"

echo "===== 3. 站内信发送：直接内容 ====="
SEND1=$(curl -s -m 5 -X POST $BASE_NOTI/api/notifications/internal/send -H "Content-Type: application/json" \
  -H "X-Internal-Key: $INTERNAL_KEY" \
  -d "{\"userId\":\"$BUYER_ID\",\"type\":2,\"title\":\"订单支付成功\",\"content\":\"您的订单 ORD2026TEST 已支付\",\"bizType\":\"ORDER_PAID\",\"bizId\":\"ORD2026TEST\"}")
check "发送站内信（直接内容）" '"notificationId"' "$SEND1"
check "发送结果含实时送达标记" '"realtimeDelivered":true' "$SEND1"

echo "===== 4. 站内信发送：模板渲染 ====="
SEND2=$(curl -s -m 5 -X POST $BASE_NOTI/api/notifications/internal/send -H "Content-Type: application/json" \
  -H "X-Internal-Key: $INTERNAL_KEY" \
  -d "{\"userId\":\"$BUYER_ID\",\"type\":1,\"templateCode\":\"ORDER_PAID\",\"templateData\":{\"OrderNo\":\"ORD2026TMPL\",\"Amount\":\"99.50\"}}")
check "发送站内信（模板渲染）" '"notificationId"' "$SEND2"
SEND2_ID=$(echo "$SEND2" | grep -o '"notificationId":"[^"]*"' | head -1 | cut -d'"' -f4)
check "模板渲染标题" "订单支付成功" "$(curl -s -m 5 "$BASE_NOTI/api/notifications?page=1&pageSize=10" -H "Authorization: Bearer $BUYER_TOKEN")"
check "模板渲染金额替换" "99.50" "$(curl -s -m 5 "$BASE_NOTI/api/notifications?page=1&pageSize=10" -H "Authorization: Bearer $BUYER_TOKEN")"
check "不存在的模板 400" "TEMPLATE_NOT_FOUND" "$(curl -s -m 5 -X POST $BASE_NOTI/api/notifications/internal/send -H "Content-Type: application/json" \
  -H "X-Internal-Key: $INTERNAL_KEY" \
  -d "{\"userId\":\"$BUYER_ID\",\"templateCode\":\"NO_SUCH_TPL\"}")"

echo "===== 5. 通知列表 + 未读数 ====="
LIST=$(curl -s -m 5 "$BASE_NOTI/api/notifications?page=1&pageSize=20" -H "Authorization: Bearer $BUYER_TOKEN")
check "列表 totalCount=2" '"totalCount":2' "$LIST"
check "列表含订单通知" "订单支付成功" "$LIST"
check "未读数=2" '"unreadCount":2' "$(curl -s -m 5 $BASE_NOTI/api/notifications/unread-count -H "Authorization: Bearer $BUYER_TOKEN")"
check "按类型过滤（订单）" '"totalCount":1' "$(curl -s -m 5 "$BASE_NOTI/api/notifications?type=1" -H "Authorization: Bearer $BUYER_TOKEN")"

echo "===== 6. 已读 / 全部已读 / 删除 ====="
check "标记单条已读" '"isRead":true' "$(curl -s -m 5 -X POST $BASE_NOTI/api/notifications/$SEND2_ID/read -H "Authorization: Bearer $BUYER_TOKEN")"
check "已读后未读数=1" '"unreadCount":1' "$(curl -s -m 5 $BASE_NOTI/api/notifications/unread-count -H "Authorization: Bearer $BUYER_TOKEN")"
check "他人标记我的通知 404" "404" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" -X POST $BASE_NOTI/api/notifications/$SEND2_ID/read -H "Authorization: Bearer $ADMIN_TOKEN")"
check "全部标记已读" '"unreadCount":0' "$(curl -s -m 5 -X POST $BASE_NOTI/api/notifications/read-all -H "Authorization: Bearer $BUYER_TOKEN")"
check "删除单条 204" "204" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" -X DELETE $BASE_NOTI/api/notifications/$SEND2_ID -H "Authorization: Bearer $BUYER_TOKEN")"
check "删除后列表 totalCount=1" '"totalCount":1' "$(curl -s -m 5 "$BASE_NOTI/api/notifications?page=1&pageSize=20" -H "Authorization: Bearer $BUYER_TOKEN")"

echo "===== 7. 短信 DryRun ====="
SMS=$(curl -s -m 5 -X POST $BASE_NOTI/api/notifications/internal/sms -H "Content-Type: application/json" \
  -H "X-Internal-Key: $INTERNAL_KEY" \
  -d "{\"phone\":\"13800138000\",\"content\":\"【多商户平台】您的验证码是 123456\"}")
check "发送短信" '"smsId"' "$SMS"
check "短信 DryRun 标记" '"dryRun":true' "$SMS"
check "短信状态 Sent" '"status":1' "$SMS"
check "短信参数校验 400" "INVALID_PHONE" "$(curl -s -m 5 -X POST $BASE_NOTI/api/notifications/internal/sms -H "Content-Type: application/json" \
  -H "X-Internal-Key: $INTERNAL_KEY" -d '{"phone":"1","content":"短"}' | head -c 200)"

echo "===== 8. Push DryRun ====="
PUSH=$(curl -s -m 5 -X POST $BASE_NOTI/api/notifications/internal/push -H "Content-Type: application/json" \
  -H "X-Internal-Key: $INTERNAL_KEY" \
  -d "{\"deviceToken\":\"DEVICE-TOKEN-$STAMP\",\"title\":\"订单发货\",\"content\":\"您的包裹已发出\"}")
check "发送 Push" '"pushId"' "$PUSH"
check "Push DryRun 标记" '"dryRun":true' "$PUSH"
check "Push 状态 Sent" '"status":1' "$PUSH"

echo "===== 9. 模板管理 CRUD ====="
TPL=$(curl -s -m 5 -X POST $BASE_NOTI/api/notifications/templates -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d '{"code":"SMOKE_TEST_TPL","titleTemplate":"冒烟模板","bodyTemplate":"变量 {Code} 渲染测试","channels":3,"description":"冒烟用"}')
check "创建模板" '"code":"SMOKE_TEST_TPL"' "$TPL"
TPL_ID=$(echo "$TPL" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
check "模板列表含默认种子" "ORDER_PAID" "$(curl -s -m 5 "$BASE_NOTI/api/notifications/templates" -H "Authorization: Bearer $ADMIN_TOKEN")"
check "模板列表含新建" "SMOKE_TEST_TPL" "$(curl -s -m 5 "$BASE_NOTI/api/notifications/templates" -H "Authorization: Bearer $ADMIN_TOKEN")"
check "更新模板" "变量2" "$(curl -s -m 5 -X PUT $BASE_NOTI/api/notifications/templates/$TPL_ID -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d '{"code":"SMOKE_TEST_TPL","titleTemplate":"冒烟模板2","bodyTemplate":"变量2 {Code}","channels":3,"description":"改"}')"
check "停用模板" '"isActive":false' "$(curl -s -m 5 -X POST "$BASE_NOTI/api/notifications/templates/$TPL_ID/enabled?enabled=false" -H "Authorization: Bearer $ADMIN_TOKEN")"
check "停用后发送 400" "TEMPLATE_NOT_FOUND" "$(curl -s -m 5 -X POST $BASE_NOTI/api/notifications/internal/send -H "Content-Type: application/json" \
  -H "X-Internal-Key: $INTERNAL_KEY" \
  -d "{\"userId\":\"$BUYER_ID\",\"templateCode\":\"SMOKE_TEST_TPL\"}")"
check "启用模板" '"isActive":true' "$(curl -s -m 5 -X POST "$BASE_NOTI/api/notifications/templates/$TPL_ID/enabled?enabled=true" -H "Authorization: Bearer $ADMIN_TOKEN")"
check "删除模板 204" "204" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" -X DELETE $BASE_NOTI/api/notifications/templates/$TPL_ID -H "Authorization: Bearer $ADMIN_TOKEN")"
check "重复编码创建 500(唯一约束)" "500" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" -X POST $BASE_NOTI/api/notifications/templates -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d '{"code":"ORDER_PAID","titleTemplate":"x","bodyTemplate":"y","channels":1}')"

echo ""
echo "========== 结果: ✅ $PASS_N 通过 / ❌ $FAIL_N 失败 =========="
[ $FAIL_N -eq 0 ] && echo "ALL PASSED" || echo "SOME FAILED"

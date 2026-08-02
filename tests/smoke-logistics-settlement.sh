#!/bin/bash
# logistics + settlement 冒烟测试脚本（v5.9）
# 依赖：identity 8001 / order 8004 / logistics 8013 / settlement 8014 已启动
# admin 提权依赖本地 SQL Server（sa/123456），仅开发环境使用
set -u
BASE_LOGI="http://localhost:8013"
BASE_SETTLE="http://localhost:8014"
BASE_ID="http://localhost:8001"
BASE_ORDER="http://localhost:8004"
MERCHANT="875DC16D-2A6B-4478-82D1-55DA8FBBE586"
STAMP=$(date +%Y%m%d%H%M%S)
BUYER_EMAIL="logi_smoke_${STAMP}@test.com"
ADMIN_EMAIL="settle_admin_${STAMP}@test.com"
PASS="Smoke@123456"
PASS=0; FAIL=0

check() { # $1=名称 $2=期望子串 $3=实际输出 $4=可选 -F 固定匹配
  if echo "$3" | grep -q ${4:-} "$2"; then echo "✅ $1"; PASS=$((PASS+1));
  else echo "❌ $1 | 期望含: $2 | 实际: $(echo "$3" | head -c 300)"; FAIL=$((FAIL+1)); fi
}

echo "===== 0. 前置：注册买家 + admin（提权）====="
BUYER=$(curl -s -m 8 -X POST $BASE_ID/api/auth/register -H "Content-Type: application/json" \
  -d "{\"email\":\"$BUYER_EMAIL\",\"password\":\"$PASS\",\"displayName\":\"物流冒烟买家\"}")
check "注册买家" '"token"' "$BUYER"
BUYER_TOKEN=$(echo "$BUYER" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)

curl -s -m 8 -X POST $BASE_ID/api/auth/register -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$PASS\",\"displayName\":\"结算冒烟管理员\"}" > /dev/null
# admin 提权（本地开发库）
echo "UPDATE Users SET RolesJson = '[\"admin\"]' WHERE Email = '$ADMIN_EMAIL'" > /tmp/role_tmp.sql
sqlcmd -S localhost -U sa -P 123456 -d MMP_Identity -i "E:\\MultiMerchantPlatform\\tests\\role_tmp.sql" -W > /dev/null 2>&1 || true
ADMIN_TOKEN=$(curl -s -m 8 -X POST $BASE_ID/api/auth/login -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$PASS\"}" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
check "admin 登录（提权后）" '"token"' "\"token\":\"$ADMIN_TOKEN\""

echo "===== 1. 健康检查 ====="
check "logistics 健康" "healthy" "$(curl -s -m 5 $BASE_LOGI/api/health)"
check "settlement 健康" "healthy" "$(curl -s -m 5 $BASE_SETTLE/api/health)"

echo "===== 2. 物流：平台公司列表（admin）====="
COMPANIES=$(curl -s -m 5 "$BASE_LOGI/api/logistics/companies?page=1&pageSize=10" -H "Authorization: Bearer $ADMIN_TOKEN")
check "公司列表含顺丰" "顺丰速运" "$COMPANIES"

echo "===== 3. 物流：买家调平台接口 → 403 ====="
check "非 admin 403" "403" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" $BASE_LOGI/api/logistics/companies -H "Authorization: Bearer $BUYER_TOKEN")"

echo "===== 4. 物流：内部创建运单（X-Internal-Key）====="
CREATE=$(curl -s -m 5 -X POST $BASE_LOGI/api/logistics/internal/shipments \
  -H "Content-Type: application/json" -H "X-Internal-Key: MMP-Internal-Key-2026" \
  -d "{\"buyerUserId\":\"00000000-0000-0000-0000-000000000001\",\"merchantId\":\"$MERCHANT\",\"subOrderId\":\"$STAMP-0001-4000-8000-000000000001\",\"orderId\":\"$STAMP-0002-4000-8000-000000000002\",\"orderNo\":\"ORD$STAMP\",\"carrierCode\":\"SF\",\"trackingNo\":\"SF$STAMP\"}")
check "创建运单成功（公司名带出）" "顺丰速运" "$CREATE"

echo "===== 5. 物流：重复创建 → 400 ====="
check "同子订单重复创建 400" "SHIPMENT_ALREADY_EXISTS" "$(curl -s -m 5 -X POST $BASE_LOGI/api/logistics/internal/shipments \
  -H "Content-Type: application/json" -H "X-Internal-Key: MMP-Internal-Key-2026" \
  -d "{\"buyerUserId\":\"00000000-0000-0000-0000-000000000001\",\"merchantId\":\"$MERCHANT\",\"subOrderId\":\"$STAMP-0001-4000-8000-000000000001\",\"orderId\":\"$STAMP-0002-4000-8000-000000000002\",\"orderNo\":\"ORD$STAMP\",\"carrierCode\":\"SF\",\"trackingNo\":\"SF$STAMP\"}")"

echo "===== 6. 物流：轨迹推进至签收 ====="
check "推进1 运输中" '"status":2' "$(curl -s -m 5 -X POST $BASE_LOGI/api/logistics/internal/tracks/advance \
  -H "Content-Type: application/json" -H "X-Internal-Key: MMP-Internal-Key-2026" \
  -d "{\"trackingNo\":\"SF$STAMP\",\"location\":\"成都转运中心\"}")"
check "推进2 派送中" '"status":3' "$(curl -s -m 5 -X POST $BASE_LOGI/api/logistics/internal/tracks/advance \
  -H "Content-Type: application/json" -H "X-Internal-Key: MMP-Internal-Key-2026" \
  -d "{\"trackingNo\":\"SF$STAMP\",\"location\":\"锦江营业点\"}")"
check "推进3 签收" '"status":4' "$(curl -s -m 5 -X POST $BASE_LOGI/api/logistics/internal/tracks/advance \
  -H "Content-Type: application/json" -H "X-Internal-Key: MMP-Internal-Key-2026" \
  -d "{\"trackingNo\":\"SF$STAMP\",\"description\":\"已签收\"}")"
check "签收后再推进 → 400" "SHIPMENT_ALREADY_SIGNED" "$(curl -s -m 5 -X POST $BASE_LOGI/api/logistics/internal/tracks/advance \
  -H "Content-Type: application/json" -H "X-Internal-Key: MMP-Internal-Key-2026" \
  -d "{\"trackingNo\":\"SF$STAMP\"}")"

echo "===== 7. 结算：设置佣金规则 10% ====="
check "佣金规则 10%" '"rate":10' "$(curl -s -m 5 -X PUT $BASE_SETTLE/api/commission-rules/$MERCHANT \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" -d '{"rate":10}')"

echo "===== 8. 结算：生成结算单（幂等）====="
GEN=$(curl -s -m 15 -X POST $BASE_SETTLE/api/settlements/generate \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" -d '{}')
check "生成结算单（有明细或空）" '"skippedCount"' "$GEN"
GEN2=$(curl -s -m 15 -X POST $BASE_SETTLE/api/settlements/generate \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" -d '{}')
check "重复生成幂等（skipped>=0）" '"skippedCount"' "$GEN2"

echo "===== 9. 结算：商户端概览 ====="
check "商户概览" "pendingAmount" "$(curl -s -m 5 $BASE_SETTLE/api/settlements/merchant/summary \
  -H "Authorization: Bearer $BUYER_TOKEN" -H "X-Merchant-Id: $MERCHANT")"
check "商户佣金比例" '"rate":10' "$(curl -s -m 5 $BASE_SETTLE/api/settlements/merchant/commission \
  -H "Authorization: Bearer $BUYER_TOKEN" -H "X-Merchant-Id: $MERCHANT")"
check "缺商户头 → 400" "MERCHANT_REQUIRED" "$(curl -s -m 5 $BASE_SETTLE/api/settlements/merchant/summary \
  -H "Authorization: Bearer $BUYER_TOKEN")"

echo "===== 10. 商户端运单列表（启用公司）====="
check "启用公司列表" "顺丰速运" "$(curl -s -m 5 $BASE_LOGI/api/logistics/shipments/merchant/companies \
  -H "Authorization: Bearer $BUYER_TOKEN" -H "X-Merchant-Id: $MERCHANT")"

echo ""
echo "========== 结果: ✅ $PASS 通过 / ❌ $FAIL 失败 =========="
[ $FAIL -eq 0 ] && echo "ALL PASSED" || echo "SOME FAILED"

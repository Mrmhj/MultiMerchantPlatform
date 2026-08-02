#!/bin/bash
# promotion-service 冒烟测试脚本（v5.7）
# 依赖：promotion 8009 / identity 8001 / gateway 8000 已启动
set -u
BASE_PROMO="http://localhost:8009"
BASE_GW="http://localhost:8000"
BASE_ID="http://localhost:8001"
MERCHANT="11111111-2222-3333-4444-555555555555"
TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI4MGU0NGNmZC02M2MzLTQ1YTItYWMxYy1jYWExMTQ1YmVhMDEiLCJ1bmlxdWVfbmFtZSI6InByb21vX3Ntb2tlXzIwMjYwODAyQHRlc3QuY29tIiwianRpIjoiZWVkNzZhNTEtNTUzYi00NTRkLWJiNjAtYzUyZGQ0ZjQ2N2FjIiwicm9sZSI6ImN1c3RvbWVyIiwiZXhwIjoxNzg1NjYyMjQxLCJpc3MiOiJNdWx0aU1lcmNoYW50UGxhdGZvcm0iLCJhdWQiOiJNdWx0aU1lcmNoYW50UGxhdGZvcm0gQ2xpZW50cyJ9.fiu9rLAHk1fNVvgu4W075O7Pv_HKPCmI9oP8yl0kPnk"
PASS=0; FAIL=0

check() { # $1=名称 $2=期望子串 $3=实际输出 $4=可选 -F 固定匹配
  if echo "$3" | grep -q ${4:-} "$2"; then echo "✅ $1"; PASS=$((PASS+1));
  else echo "❌ $1 | 期望含: $2 | 实际: $(echo "$3" | head -c 300)"; FAIL=$((FAIL+1)); fi
}

echo "===== 1. 健康检查 ====="
check "健康检查 promotion" "healthy" "$(curl -s -m 5 $BASE_PROMO/api/health)"

echo "===== 2. 商户创建优惠券（满100减20，限量100，限领1）====="
CREATE=$(curl -s -m 5 -X POST $BASE_PROMO/api/promotion/coupons \
  -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT" -H "Content-Type: application/json" \
  -d '{"name":"满100减20券","thresholdAmount":100,"discountAmount":20,"totalQuantity":100,"limitPerUser":1,"validFrom":"2026-08-01T00:00:00Z","validUntil":"2026-12-31T23:59:59Z"}')
check "创建优惠券 201" "满100减20券" "$CREATE"
COUPON_ID=$(echo "$CREATE" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
echo "    couponId=$COUPON_ID"

echo "===== 3. 缺商户头创建券 → 400 ====="
check "缺 X-Merchant-Id → 400" "MERCHANT_REQUIRED" "$(curl -s -m 5 -X POST $BASE_PROMO/api/promotion/coupons \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"无商户券","thresholdAmount":50,"discountAmount":10,"totalQuantity":10,"limitPerUser":1,"validFrom":"2026-08-01T00:00:00Z","validUntil":"2026-12-31T23:59:59Z"}')"

echo "===== 4. 商户券列表（分页）====="
check "券列表包含" "满100减20券" "$(curl -s -m 5 "$BASE_PROMO/api/promotion/coupons?page=1&pageSize=10" \
  -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT")"

echo "===== 5. C 端可领券列表（公开）====="
check "可领券列表" "满100减20券" "$(curl -s -m 5 $BASE_PROMO/api/promotion/coupons/available)"

echo "===== 6. 买家领券 ====="
CLAIM=$(curl -s -m 5 -X POST $BASE_PROMO/api/promotion/coupons/$COUPON_ID/claim \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{}')
check "领券成功" "满100减20券" "$CLAIM"
USER_COUPON_ID=$(echo "$CLAIM" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
echo "    userCouponId=$USER_COUPON_ID"

echo "===== 7. 重复领券 → 400（限领1）====="
check "重复领券 → LIMIT_REACHED" "LIMIT_REACHED" "$(curl -s -m 5 -X POST $BASE_PROMO/api/promotion/coupons/$COUPON_ID/claim \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{}')"

echo "===== 8. 我的优惠券 ====="
check "我的券" "满100减20券" "$(curl -s -m 5 "$BASE_PROMO/api/promotion/my/coupons?status=unused" \
  -H "Authorization: Bearer $TOKEN")"

echo "===== 9. 内部核销：密钥错误 → 401 ====="
check "错误内部密钥 → 401" "内部密钥无效" "$(curl -s -m 5 -X POST $BASE_PROMO/api/promotion/internal/coupons/use \
  -H "X-Internal-Key: WRONG-KEY" -H "Content-Type: application/json" \
  -d "{\"userId\":\"80e44cfd-63c3-45a2-ac1c-caa1145bea01\",\"userCouponId\":\"$USER_COUPON_ID\",\"orderId\":\"a0000000-0000-0000-0000-000000000001\"}")"

echo "===== 10. 内部核销：正确密钥 → 成功（优惠20）====="
USE=$(curl -s -m 5 -X POST $BASE_PROMO/api/promotion/internal/coupons/use \
  -H "X-Internal-Key: MMP-Internal-Key-2026" -H "Content-Type: application/json" \
  -d "{\"userId\":\"80e44cfd-63c3-45a2-ac1c-caa1145bea01\",\"userCouponId\":\"$USER_COUPON_ID\",\"orderId\":\"a0000000-0000-0000-0000-000000000001\"}")
check "核销成功" '"success":true' "$USE"
check "核销金额20" '"discountAmount":20' "$USE"

echo "===== 11. 重复核销 → 幂等成功 ====="
check "重复核销幂等" '"success":true' "$(curl -s -m 5 -X POST $BASE_PROMO/api/promotion/internal/coupons/use \
  -H "X-Internal-Key: MMP-Internal-Key-2026" -H "Content-Type: application/json" \
  -d "{\"userId\":\"80e44cfd-63c3-45a2-ac1c-caa1145bea01\",\"userCouponId\":\"$USER_COUPON_ID\"}")"

echo "===== 12. 我的券 status=used ====="
check "已使用过滤" '"status":2' "$(curl -s -m 5 "$BASE_PROMO/api/promotion/my/coupons?status=used" \
  -H "Authorization: Bearer $TOKEN")"

echo "===== 13. 商户创建满减活动 ====="
ACT=$(curl -s -m 5 -X POST $BASE_PROMO/api/promotion/activities \
  -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT" -H "Content-Type: application/json" \
  -d '{"name":"暑期满300减50","thresholdAmount":300,"discountAmount":50,"startTime":"2026-08-01T00:00:00Z","endTime":"2026-08-31T23:59:59Z"}')
check "创建活动（Draft）" '"status":1' "$ACT"
ACT_ID=$(echo "$ACT" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
echo "    activityId=$ACT_ID"

echo "===== 14. 启用活动 ====="
check "启用活动 → Active" '"status":2' "$(curl -s -m 5 -X PUT $BASE_PROMO/api/promotion/activities/$ACT_ID/status \
  -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT" -H "Content-Type: application/json" \
  -d '{"active":true}')"

echo "===== 15. C 端进行中活动（公开）====="
check "进行中活动" "暑期满300减50" "$(curl -s -m 5 $BASE_PROMO/api/promotion/activities/active)"

echo "===== 16. 停用活动 ====="
check "停用活动 → Draft" '"status":1' "$(curl -s -m 5 -X PUT $BASE_PROMO/api/promotion/activities/$ACT_ID/status \
  -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT" -H "Content-Type: application/json" \
  -d '{"active":false}')"

echo "===== 17. 停用后 C 端进行中列表不再包含 ====="
check "进行中列表为空" "[]" "$(curl -s -m 5 $BASE_PROMO/api/promotion/activities/active)" "-F"

echo "===== 18. 网关转发：可领券列表 ====="
check "网关转发可领券" "满100减20券" "$(curl -s -m 5 $BASE_GW/api/promotion/coupons/available)"

echo ""
echo "================ 冒烟结果: 通过 $PASS / 失败 $FAIL ================"
exit $FAIL

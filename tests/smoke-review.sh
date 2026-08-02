#!/bin/bash
# review-service 冒烟测试脚本（v5.8）
# 依赖：review 8012 / identity 8001 / gateway 8000 已启动
set -u
BASE_RV="http://localhost:8012"
BASE_GW="http://localhost:8000"
BASE_ID="http://localhost:8001"
MERCHANT="33333333-4444-5555-6666-777777777777"
MERCHANT_B="88888888-9999-aaaa-bbbb-cccccccccccc"
PRODUCT="aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
PASS=0; FAIL=0

check() { # $1=名称 $2=期望子串 $3=实际输出 $4=可选 -F 固定匹配
  if echo "$3" | grep -q ${4:-} "$2"; then echo "✅ $1"; PASS=$((PASS+1));
  else echo "❌ $1 | 期望含: $2 | 实际: $(echo "$3" | head -c 300)"; FAIL=$((FAIL+1)); fi
}

# 清理历史评价数据（可重复执行；sqlcmd 不可用时跳过）
sqlcmd -S localhost -U sa -P 123456 -d MMP_Review -Q "DELETE FROM Reviews;" -b >/dev/null 2>&1 && echo "（已清理历史评价数据）" || echo "（跳过数据清理）"

echo "===== 1. 登录/注册买家（可重复执行）====="
EMAIL="review_smoke_20260802@test.com"
LOGIN=$(curl -s -m 5 -X POST $BASE_ID/api/auth/login -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"Passw0rd!2026\"}")
if echo "$LOGIN" | grep -q '"token"'; then
  TOKEN=$(echo "$LOGIN" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
  check "登录已有用户" '"token"' "$LOGIN"
else
  REG=$(curl -s -m 5 -X POST $BASE_ID/api/auth/register -H "Content-Type: application/json" \
    -d "{\"email\":\"$EMAIL\",\"password\":\"Passw0rd!2026\",\"displayName\":\"评价冒烟买家\"}")
  check "注册新用户" '"token"' "$REG"
  TOKEN=$(echo "$REG" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
fi
echo "    token 已获取"

echo "===== 2. 创建评价（5星）====="
R1=$(curl -s -m 5 -X POST $BASE_RV/api/reviews \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d "{\"merchantId\":\"$MERCHANT\",\"orderId\":\"c0000000-0000-0000-0000-000000000001\",\"subOrderId\":\"c0000000-0000-0000-0000-000000000011\",\"productId\":\"$PRODUCT\",\"productName\":\"全麦面包\",\"skuId\":\"c0000000-0000-0000-0000-000000000021\",\"skuSpec\":\"500g\",\"rating\":5,\"content\":\"面包松软，物流快，非常满意！\",\"isAnonymous\":false}")
check "创建评价 201" "全麦面包" "$R1"

echo "===== 3. 同订单商品重复评价 → 400 ====="
check "重复评价拦截" "REVIEW_ALREADY_EXISTS" "$(curl -s -m 5 -X POST $BASE_RV/api/reviews \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d "{\"merchantId\":\"$MERCHANT\",\"orderId\":\"c0000000-0000-0000-0000-000000000001\",\"subOrderId\":\"c0000000-0000-0000-0000-000000000011\",\"productId\":\"$PRODUCT\",\"productName\":\"全麦面包\",\"skuId\":\"c0000000-0000-0000-0000-000000000021\",\"skuSpec\":\"500g\",\"rating\":4,\"content\":\"第二次评价\",\"isAnonymous\":false}")"

echo "===== 4. 无效评分（6星）→ 400（ModelState 校验）====="
check "无效评分拦截" '"status":400' "$(curl -s -m 5 -X POST $BASE_RV/api/reviews \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d "{\"merchantId\":\"$MERCHANT\",\"orderId\":\"c0000000-0000-0000-0000-000000000002\",\"subOrderId\":\"c0000000-0000-0000-0000-000000000012\",\"productId\":\"$PRODUCT\",\"productName\":\"全麦面包\",\"skuId\":\"c0000000-0000-0000-0000-000000000021\",\"skuSpec\":\"500g\",\"rating\":6,\"content\":\"测试\",\"isAnonymous\":false}")"

echo "===== 5. 买家创建第二条评价（匿名，3星，不同子订单）====="
R2=$(curl -s -m 5 -X POST $BASE_RV/api/reviews \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d "{\"merchantId\":\"$MERCHANT\",\"orderId\":\"c0000000-0000-0000-0000-000000000002\",\"subOrderId\":\"c0000000-0000-0000-0000-000000000012\",\"productId\":\"$PRODUCT\",\"productName\":\"全麦面包\",\"skuId\":\"c0000000-0000-0000-0000-000000000022\",\"skuSpec\":\"1kg\",\"rating\":3,\"content\":\"包装一般，面包还不错\",\"isAnonymous\":true}")
check "第二条评价（匿名3星）" '"rating":3' "$R2"

echo "===== 6. 我的评价（2条）====="
check "我的评价总数2" '"totalCount":2' "$(curl -s -m 5 "$BASE_RV/api/reviews/my?page=1&pageSize=10" -H "Authorization: Bearer $TOKEN")"

echo "===== 7. 商户评价列表（缺商户头 → 400）====="
check "缺 X-Merchant-Id → 400" "MERCHANT_REQUIRED" "$(curl -s -m 5 "$BASE_RV/api/reviews/merchant" -H "Authorization: Bearer $TOKEN")"

echo "===== 8. 商户评价列表（2条）====="
check "商户列表总数2" '"totalCount":2' "$(curl -s -m 5 "$BASE_RV/api/reviews/merchant?page=1&pageSize=10" \
  -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT")"

echo "===== 9. 跨商户隔离（商户B 列表为空）====="
check "商户B 空列表" '"totalCount":0' "$(curl -s -m 5 "$BASE_RV/api/reviews/merchant" \
  -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT_B")"

echo "===== 10. 公开商品评价：评分统计（5+3=8/2=4，共2条）====="
PUB=$(curl -s -m 5 "$BASE_RV/api/reviews/product/$PRODUCT?page=1&pageSize=10")
check "平均分4" '"averageRating":4' "$PUB"
check "总数2" '"totalCount":2' "$PUB"
check "5星分布1" '"5":1' "$PUB"

echo "===== 11. 公开列表评分过滤（rating=5 → 1条）====="
check "5星过滤1条" '"totalCount":1' "$(curl -s -m 5 "$BASE_RV/api/reviews/product/$PRODUCT?rating=5")"

echo "===== 12. 商户回复评价 ====="
REV_ID=$(echo "$R1" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
check "回复成功" "感谢您的支持" "$(curl -s -m 5 -X PUT $BASE_RV/api/reviews/$REV_ID/reply \
  -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT" -H "Content-Type: application/json" \
  -d '{"reply":"感谢您的支持，欢迎再次光临！"}')"

echo "===== 13. 隐藏评价 → 公开统计变化（3星那条隐藏）====="
R2_ID=$(echo "$R2" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
HIDE=$(curl -s -m 5 -X PUT $BASE_RV/api/reviews/$R2_ID/status \
  -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT" -H "Content-Type: application/json" \
  -d '{"visible":false}')
check "隐藏成功" '"status":2' "$HIDE"
check "隐藏后公开仅1条" '"totalCount":1' "$(curl -s -m 5 "$BASE_RV/api/reviews/product/$PRODUCT")"
check "隐藏后平均分5" '"averageRating":5' "$(curl -s -m 5 "$BASE_RV/api/reviews/product/$PRODUCT")"

echo "===== 14. 商户列表状态过滤（hidden → 1条）====="
check "hidden 过滤1条" '"totalCount":1' "$(curl -s -m 5 "$BASE_RV/api/reviews/merchant?status=hidden" \
  -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT")"

echo "===== 15. 恢复可见 ====="
check "恢复可见" '"status":1' "$(curl -s -m 5 -X PUT $BASE_RV/api/reviews/$R2_ID/status \
  -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT" -H "Content-Type: application/json" \
  -d '{"visible":true}')"

echo "===== 16. 网关转发（公开商品评价）====="
check "网关转发" '"totalCount":2' "$(curl -s -m 5 "$BASE_GW/api/reviews/product/$PRODUCT")"

echo ""
echo "================ 冒烟结果: 通过 $PASS / 失败 $FAIL ================"
exit $FAIL

#!/bin/bash
# web-merchant 冒烟测试脚本（v6.1）
# 依赖：identity 8001 / merchant 8002 / product 8003 / im 8016 / gateway 8000 已启动
#       web-merchant dev server（5174）已启动 —— 全部请求经 Vite 代理（模拟浏览器真实链路）
# admin 提权依赖本地 SQL Server（sa/123456），仅开发环境使用
set -u
BASE="http://localhost:5174/api"
STAMP=$(date +%Y%m%d%H%M%S)
EMAIL="wm_smoke_${STAMP}@test.com"
ADMIN_EMAIL="wm_admin_${STAMP}@test.com"
PASSWD="Smoke@123456"
PASSED=0; FAILED=0

check() { # $1=名称 $2=期望子串 $3=实际输出
  if echo "$3" | grep -q "$2"; then echo "✅ $1"; PASSED=$((PASSED+1));
  else echo "❌ $1 | 期望含: $2 | 实际: $(echo "$3" | head -c 300)"; FAILED=$((FAILED+1)); fi
}

echo "===== 1. 注册商户用户 + 登录（经网关 /identity/auth/login）====="
REG=$(curl -s -m 8 -X POST $BASE/identity/auth/register -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWD\",\"displayName\":\"商户冒烟用户\"}")
check "注册" '"token"' "$REG"
LOGIN=$(curl -s -m 8 -X POST $BASE/identity/auth/login -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASSWD\"}")
TOKEN=$(echo "$LOGIN" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
USER_ID=$(echo "$LOGIN" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
check "登录" '"token"' "$LOGIN"
echo "    userId=$USER_ID"

echo "===== 2. 未入驻状态查询（/merchant/merchants/me）====="
ME=$(curl -s -m 5 -o /dev/null -w "%{http_code}" "$BASE/merchant/merchants/me" -H "Authorization: Bearer $TOKEN")
check "未入驻接口 204（无商户）" "204" "$ME"

echo "===== 3. 入驻申请（/merchant/merchants/apply）====="
APPLY=$(curl -s -m 5 -X POST $BASE/merchant/merchants/apply -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d "{\"name\":\"冒烟食品旗舰店$STAMP\",\"licenseNo\":\"91510100$STAMP\",\"contactName\":\"张三\",\"contactPhone\":\"13800000000\",\"description\":\"冒烟测试店铺\"}")
check "申请成功（待审 status=1）" '"status":1' "$APPLY"
MERCHANT_ID=$(echo "$APPLY" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
echo "    merchantId=$MERCHANT_ID"

echo "===== 4. admin 提权 + 审核通过 ====="
curl -s -m 8 -X POST $BASE/identity/auth/register -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$PASSWD\",\"displayName\":\"冒烟管理员\"}" > /dev/null
cat > "E:/MultiMerchantPlatform/tests/role_tmp.sql" << SQL
UPDATE Users SET RolesJson = '["admin"]' WHERE Email = '$ADMIN_EMAIL'
SQL
cd "E:/MultiMerchantPlatform/tests" && sqlcmd -S localhost -U sa -P 123456 -d MMP_Identity -i role_tmp.sql -W > /dev/null 2>&1 || echo "（sqlcmd 提权失败，跳过审核步骤）"
ADMIN_TOKEN=$(curl -s -m 8 -X POST $BASE/identity/auth/login -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$PASSWD\"}" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
if [ -n "$ADMIN_TOKEN" ]; then
  REVIEW=$(curl -s -m 5 -X POST "$BASE/merchant/merchants/$MERCHANT_ID/review" -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
    -d '{"approved":true}')
  check "审核通过（status=2）" '"status":2' "$REVIEW"
else
  check "admin 登录（提权）" "skip" ""
fi

echo "===== 5. 商户信息回查（含 merchantId）====="
ME2=$(curl -s -m 5 "$BASE/merchant/merchants/me" -H "Authorization: Bearer $TOKEN")
check "商户状态已通过" '"status":2' "$ME2"
MH="-H \"Authorization: Bearer $TOKEN\" -H \"X-Merchant-Id: $MERCHANT_ID\""

echo "===== 6. 商品分类创建 + 列表 ====="
CAT=$(curl -s -m 5 -X POST $BASE/product/categories -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT_ID" -H "Content-Type: application/json" \
  -d '{"name":"食品饮料","sortOrder":1,"isActive":true}')
check "创建分类" "食品饮料" "$CAT"
CAT_ID=$(echo "$CAT" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
CATS=$(curl -s -m 5 "$BASE/product/categories" -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT_ID")
check "分类列表" "食品饮料" "$CATS"

echo "===== 7. 创建商品（含 SKU）====="
PROD=$(curl -s -m 5 -X POST $BASE/product/products -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT_ID" -H "Content-Type: application/json" \
  -d "{\"name\":\"冒烟全麦面包\",\"categoryId\":\"$CAT_ID\",\"description\":\"冒烟商品\",\"skus\":[{\"skuCode\":\"BREAD-500G\",\"spec\":\"500g\",\"price\":19.9,\"stock\":100}]}")
check "创建商品（草稿 status=1）" '"status":1' "$PROD"
PROD_ID=$(echo "$PROD" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
echo "    productId=$PROD_ID"

echo "===== 8. 商品上架 + 列表 ====="
UP=$(curl -s -m 5 -X PUT "$BASE/product/products/$PROD_ID/status" -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT_ID" -H "Content-Type: application/json" -d '{"status":2}')
check "上架成功（status=2）" '"status":2' "$UP"
PLIST=$(curl -s -m 5 "$BASE/product/products?page=1&pageSize=10" -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT_ID")
check "商品列表含面包" "冒烟全麦面包" "$PLIST"

echo "===== 9. 库存列表 ====="
STOCKS=$(curl -s -m 5 "$BASE/stock/stocks?page=1&pageSize=10" -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT_ID")
check "库存接口可用" '"items"' "$STOCKS"

echo "===== 10. 营销：优惠券创建 + 满减活动创建 ====="
COUPON=$(curl -s -m 5 -X POST $BASE/promotion/coupons -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT_ID" -H "Content-Type: application/json" \
  -d '{"name":"冒烟满100减20","thresholdAmount":100,"discountAmount":20,"totalQuantity":100,"limitPerUser":1,"validFrom":"2026-08-01T00:00:00Z","validUntil":"2026-12-31T23:59:59Z"}')
check "创建优惠券" "冒烟满100减20" "$COUPON"
ACT=$(curl -s -m 5 -X POST $BASE/promotion/activities -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT_ID" -H "Content-Type: application/json" \
  -d '{"name":"冒烟满200减30","thresholdAmount":200,"discountAmount":30,"startTime":"2026-08-01T00:00:00Z","endTime":"2026-12-31T23:59:59Z"}')
check "创建满减活动" "冒烟满200减30" "$ACT"

echo "===== 11. 评价列表 / 运单列表 / 结算概览 ====="
RV=$(curl -s -m 5 "$BASE/reviews/merchant?page=1&pageSize=10" -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT_ID")
check "评价列表接口" '"items"' "$RV"
SHIP=$(curl -s -m 5 "$BASE/logistics/shipments/merchant?page=1&pageSize=10" -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT_ID")
check "运单列表接口" '"items"' "$SHIP"
SUM=$(curl -s -m 5 "$BASE/settlements/merchant/summary" -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT_ID")
check "结算概览接口" '"pendingCount"' "$SUM"
COMM=$(curl -s -m 5 "$BASE/settlements/merchant/commission" -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT_ID")
check "佣金比例接口" '"rate"' "$COMM"

echo "===== 12. IM：内部推送建系统会话 → 商户会话列表 ====="
PUSH=$(curl -s -m 5 -X POST http://localhost:8016/api/im/internal/push \
  -H "X-Internal-Key: MMP-Internal-Key-2026" -H "Content-Type: application/json" \
  -d "{\"toUserId\":\"$USER_ID\",\"merchantId\":\"$MERCHANT_ID\",\"content\":\"您的订单 WM$STAMP 已发货\",\"messageType\":5}")
check "内部推送成功" '"messageId"' "$PUSH"
SESS=$(curl -s -m 5 "$BASE/im/merchant/sessions" -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT_ID")
check "商户会话列表含会话" '"unreadCount"' "$SESS"
SESS_ID=$(echo "$SESS" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
echo "    sessionId=$SESS_ID"
REPLY=$(curl -s -m 5 -X POST "$BASE/im/merchant/sessions/$SESS_ID/reply" -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT_ID" -H "Content-Type: application/json" \
  -d '{"content":"您好，您的订单已发出，请留意物流信息","messageType":1}')
check "商户回复消息" "已发出" "$REPLY"

echo ""
echo "════════════════════════════════════════"
echo "web-merchant 冒烟结果：通过 $PASSED 项，失败 $FAILED 项"
echo "════════════════════════════════════════"
[ $FAILED -eq 0 ]

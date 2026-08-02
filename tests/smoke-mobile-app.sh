#!/bin/bash
# mobile-app 移动端冒烟测试脚本（v6.2）
# 依赖：后端服务（identity/product/cart/order/pay/stock/search/im/gateway 8000）已启动
#       mobile-app H5 dev（5175）已启动 —— 全部请求经 Vite 代理（模拟 App 真实链路）
# 覆盖：注册登录 → 商品列表 → 加购 → 购物车 → 下单 → 支付 → 订单列表 → IM 会话/收发
set -u
BASE="http://localhost:5175/api"
STAMP=$(date +%Y%m%d%H%M%S)
BUYER_EMAIL="mb_buyer_${STAMP}@test.com"
STAFF_EMAIL="mb_staff_${STAMP}@test.com"
PASSWD="Smoke@123456"
PASSED=0; FAILED=0

check() { # $1=名称 $2=期望子串 $3=实际输出
  if echo "$3" | grep -q "$2"; then echo "✅ $1"; PASSED=$((PASSED+1));
  else echo "❌ $1 | 期望含: $2 | 实际: $(echo "$3" | head -c 300)"; FAILED=$((FAILED+1)); fi
}

echo "===== 1. 注册买家 A + 客服 B（经网关）====="
BUYER=$(curl -s -m 8 -X POST $BASE/identity/auth/register -H "Content-Type: application/json" \
  -d "{\"email\":\"$BUYER_EMAIL\",\"password\":\"$PASSWD\",\"displayName\":\"移动端买家\"}")
check "注册买家" '"token"' "$BUYER"
TOKEN_A=$(echo "$BUYER" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
USER_ID_A=$(echo "$BUYER" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
STAFF=$(curl -s -m 8 -X POST $BASE/identity/auth/register -H "Content-Type: application/json" \
  -d "{\"email\":\"$STAFF_EMAIL\",\"password\":\"$PASSWD\",\"displayName\":\"移动端客服\"}")
check "注册客服" '"token"' "$STAFF"
TOKEN_B=$(echo "$STAFF" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
USER_ID_B=$(echo "$STAFF" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
echo "    买家A=$USER_ID_A 客服B=$USER_ID_B"

echo "===== 2. 商品列表（C 端公开接口）====="
PRODS=$(curl -s -m 5 "$BASE/product/products/public?page=1&pageSize=12")
check "商品列表含 items" '"items"' "$PRODS"
FIRST_PRODUCT=$(echo "$PRODS" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
FIRST_SKU=$(echo "$PRODS" | grep -o '"skus":\[{"id":"[^"]*"' | head -1 | sed 's/.*"id":"//; s/"$//')
FIRST_MERCHANT=$(echo "$PRODS" | grep -o '"merchantId":"[^"]*"' | head -1 | cut -d'"' -f4)
FIRST_MERCHANT_NAME=$(echo "$PRODS" | grep -o '"merchantName":"[^"]*"' | head -1 | cut -d'"' -f4)
echo "    product=$FIRST_PRODUCT sku=$FIRST_SKU merchant=$FIRST_MERCHANT name=$FIRST_MERCHANT_NAME"

echo "===== 3.5 补库存数据（sqlcmd，保证下单链路）====="
if [ -n "$FIRST_SKU" ] && [ -n "$FIRST_MERCHANT" ]; then
  cat > "E:/MultiMerchantPlatform/tests/stock_tmp.sql" << SQL
IF NOT EXISTS (SELECT 1 FROM StockItems WHERE SkuId = '$FIRST_SKU')
  INSERT INTO StockItems (Id, MerchantId, SkuId, Total, Reserved, CreatedAt, UpdatedAt, IsDeleted)
  VALUES (NEWID(), '$FIRST_MERCHANT', '$FIRST_SKU', 100, 0, SYSUTCDATETIME(), NULL, 0)
SQL
  cd "E:/MultiMerchantPlatform/tests" && sqlcmd -S localhost -U sa -P 123456 -d MMP_Stock -i stock_tmp.sql -W > /dev/null 2>&1 && echo "✅ 库存数据就绪" && PASSED=$((PASSED+1)) || echo "❌ 补库存失败"
fi

echo "===== 3. 商品搜索（search 接口）====="
SEARCH=$(curl -s -m 5 "$BASE/search/products?keyword=%E9%9D%A2%E5%8C%85&page=1&pageSize=10")
check "搜索接口可用" '"items"' "$SEARCH"

echo "===== 4. 加购（买家 A）====="
if [ -n "$FIRST_SKU" ] && [ -n "$FIRST_MERCHANT" ]; then
  ADD=$(curl -s -m 5 -X POST $BASE/cart/items -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" \
    -d "{\"merchantId\":\"$FIRST_MERCHANT\",\"merchantName\":\"$FIRST_MERCHANT_NAME\",\"productId\":\"$FIRST_PRODUCT\",\"productName\":\"冒烟全麦面包\",\"skuId\":\"$FIRST_SKU\",\"skuCode\":\"BREAD-500G\",\"spec\":\"500g\",\"unitPrice\":19.9,\"quantity\":2}")
  check "加购成功" '"skuId"' "$ADD"
else
  check "加购成功（跳过：无商品数据）" "skip" ""
fi

echo "===== 5. 购物车列表 + 选中合计 ====="
CART=$(curl -s -m 5 "$BASE/cart" -H "Authorization: Bearer $TOKEN_A")
check "购物车含条目" '"items"' "$CART"

echo "===== 6. 下单（选中项创建订单）====="
ORDER=$(curl -s -m 5 -X POST $BASE/order/orders -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" \
  -d "{\"items\":[{\"merchantId\":\"$FIRST_MERCHANT\",\"merchantName\":\"移动端测试商户\",\"productId\":\"$FIRST_PRODUCT\",\"productName\":\"移动端冒烟商品\",\"skuId\":\"$FIRST_SKU\",\"skuCode\":\"MB-SKU\",\"spec\":\"500g\",\"unitPrice\":19.9,\"quantity\":1}]}")
check "下单成功（待付款 status=1）" '"status":1' "$ORDER"
ORDER_ID=$(echo "$ORDER" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
ORDER_NO=$(echo "$ORDER" | grep -o '"orderNo":"[^"]*"' | head -1 | cut -d'"' -f4)
echo "    order=$ORDER_ID no=$ORDER_NO"

echo "===== 7. 支付（模拟支付成功）====="
PAY=$(curl -s -m 5 -X POST $BASE/pay/payments -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" \
  -d "{\"orderId\":\"$ORDER_ID\",\"amount\":19.9}")
check "创建支付单" '"payNo"' "$PAY"
PAY_ID=$(echo "$PAY" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
PAID=$(curl -s -m 5 -X POST "$BASE/pay/payments/$PAY_ID/simulate-pay" -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" -d '{}')
check "模拟支付成功" '"status":2' "$PAID"

echo "===== 8. 订单列表（买家）====="
OLIST=$(curl -s -m 5 "$BASE/order/orders?page=1&pageSize=10" -H "Authorization: Bearer $TOKEN_A")
check "订单列表含订单" "$ORDER_ID" "$OLIST"

echo "===== 9. IM：买家创建私聊会话（与客服 B）====="
IM_SESSION=$(curl -s -m 5 -X POST $BASE/im/sessions/private -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" \
  -d "{\"merchantId\":\"99999999-8888-7777-6666-555555555555\",\"peerUserId\":\"$USER_ID_B\"}")
check "创建私聊会话" '"type":1' "$IM_SESSION"
IM_SESSION_ID=$(echo "$IM_SESSION" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
echo "    imSession=$IM_SESSION_ID"

echo "===== 10. IM：买家发送消息（REST 通道）====="
IMSG=$(curl -s -m 5 -X POST "$BASE/im/sessions/$IM_SESSION_ID/send" -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" \
  -d '{"content":"请问移动端冒烟商品有货吗？","messageType":1}')
check "买家发消息" "移动端冒烟商品有货吗" "$IMSG"

echo "===== 11. IM：客服 B 会话列表（未读=1）====="
BSESS=$(curl -s -m 5 "$BASE/im/sessions" -H "Authorization: Bearer $TOKEN_B")
check "B 会话列表未读=1" '"unreadCount":1' "$BSESS"

echo "===== 12. IM：客服 B 回复 ====="
BREPLY=$(curl -s -m 5 -X POST "$BASE/im/sessions/$IM_SESSION_ID/send" -H "Authorization: Bearer $TOKEN_B" -H "Content-Type: application/json" \
  -d '{"content":"有货的，欢迎下单～","messageType":1}')
check "客服回复" "有货的" "$BREPLY"

echo ""
echo "════════════════════════════════════════"
echo "mobile-app 冒烟结果：通过 $PASSED 项，失败 $FAILED 项"
echo "════════════════════════════════════════"
[ $FAILED -eq 0 ]

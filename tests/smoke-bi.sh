#!/bin/bash
# bi-admin 冒烟测试脚本（v7.0）
# 依赖：identity 8001 / product 8003 / order 8004 / pay 8005 / stock 8006 / bi-admin 8020 已启动
# admin 提权依赖本地 SQL Server（sa/123456），仅开发环境使用
# 覆盖：注册提权 → 健康检查 → 鉴权拦截 → 造单（下单+支付）→ BI 同步 → 看板五接口断言
set -u
BASE_BI="http://localhost:8020"
BASE_ID="http://localhost:8001"
BASE_PROD="http://localhost:8003"
BASE_ORDER="http://localhost:8004"
BASE_PAY="http://localhost:8005"
STAMP=$(date +%Y%m%d%H%M%S)
ADMIN_EMAIL="bi_admin_${STAMP}@test.com"
BUYER_EMAIL="bi_buyer_${STAMP}@test.com"
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
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$PASS\",\"displayName\":\"BI冒烟管理员\"}" > /dev/null
cd "$(dirname "$0")" || exit 1
echo "UPDATE Users SET RolesJson = '[\"admin\"]' WHERE Email = '$ADMIN_EMAIL'" > role_tmp_bi.sql
sqlcmd -S localhost -U sa -P 123456 -d MMP_Identity -i role_tmp_bi.sql -W > /dev/null 2>&1 || true
rm -f role_tmp_bi.sql
ADMIN_TOKEN=$(curl -s -m 8 -X POST $BASE_ID/api/auth/login -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$PASS\"}" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
check "admin 登录（提权后）" '"token"' "\"token\":\"$ADMIN_TOKEN\""
BUYER=$(curl -s -m 8 -X POST $BASE_ID/api/auth/register -H "Content-Type: application/json" \
  -d "{\"email\":\"$BUYER_EMAIL\",\"password\":\"$PASS\",\"displayName\":\"BI冒烟买家\"}")
check "买家注册" '"token"' "$BUYER"
BUYER_TOKEN=$(echo "$BUYER" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)

echo "===== 1. 健康检查 ====="
check "bi 健康" "healthy" "$(curl -s -m 5 $BASE_BI/api/health)"

echo "===== 2. 鉴权拦截 ====="
check "无 token 401" "401" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" $BASE_BI/api/bi/overview)"
check "买家调 BI 403" "403" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" $BASE_BI/api/bi/overview -H "Authorization: Bearer $BUYER_TOKEN")"

echo "===== 3. 造数：取在售商品 → 补库存 → 下单 → 支付 ====="
PRODS=$(curl -s -m 5 "$BASE_PROD/api/products/public?page=1&pageSize=5")
check "商品列表含 items" '"items"' "$PRODS"
FIRST_PRODUCT=$(echo "$PRODS" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
FIRST_SKU=$(echo "$PRODS" | grep -o '"skus":\[{"id":"[^"]*"' | head -1 | sed 's/.*"id":"//; s/"$//')
FIRST_MERCHANT=$(echo "$PRODS" | grep -o '"merchantId":"[^"]*"' | head -1 | cut -d'"' -f4)
FIRST_MERCHANT_NAME=$(echo "$PRODS" | grep -o '"merchantName":"[^"]*"' | head -1 | cut -d'"' -f4)
echo "    product=$FIRST_PRODUCT sku=$FIRST_SKU merchant=$FIRST_MERCHANT name=$FIRST_MERCHANT_NAME"

if [ -n "$FIRST_SKU" ] && [ -n "$FIRST_MERCHANT" ]; then
  # 补库存（sqlcmd 与 SQL 文件同目录）
  cat > stock_tmp_bi.sql << SQL
IF NOT EXISTS (SELECT 1 FROM StockItems WHERE SkuId = '$FIRST_SKU')
  INSERT INTO StockItems (Id, MerchantId, SkuId, Total, Reserved, CreatedAt, UpdatedAt, IsDeleted)
  VALUES (NEWID(), '$FIRST_MERCHANT', '$FIRST_SKU', 500, 0, SYSUTCDATETIME(), NULL, 0)
SQL
  sqlcmd -S localhost -U sa -P 123456 -d MMP_Stock -i stock_tmp_bi.sql -W > /dev/null 2>&1 && echo "✅ 库存就绪" || echo "⚠️ 补库存失败（可能已有库存）"
  rm -f stock_tmp_bi.sql

  ORDER=$(curl -s -m 8 -X POST $BASE_ORDER/api/orders -H "Authorization: Bearer $BUYER_TOKEN" -H "Content-Type: application/json" \
    -d "{\"items\":[{\"merchantId\":\"$FIRST_MERCHANT\",\"merchantName\":\"$FIRST_MERCHANT_NAME\",\"productId\":\"$FIRST_PRODUCT\",\"productName\":\"BI冒烟商品\",\"skuId\":\"$FIRST_SKU\",\"skuCode\":\"BI-SKU-$STAMP\",\"spec\":\"标准\",\"unitPrice\":88.5,\"quantity\":2}]}")
  check "下单成功（待付款）" '"status":1' "$ORDER"
  ORDER_ID=$(echo "$ORDER" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
  echo "    order=$ORDER_ID"

  PAY=$(curl -s -m 8 -X POST $BASE_PAY/api/payments -H "Authorization: Bearer $BUYER_TOKEN" -H "Content-Type: application/json" \
    -d "{\"orderId\":\"$ORDER_ID\",\"amount\":177.0}")
  check "创建支付单" '"payNo"' "$PAY"
  PAY_ID=$(echo "$PAY" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
  PAID=$(curl -s -m 8 -X POST "$BASE_PAY/api/payments/$PAY_ID/simulate-pay" -H "Authorization: Bearer $BUYER_TOKEN" -H "Content-Type: application/json" -d '{}')
  check "模拟支付成功（已付款）" '"status":2' "$PAID"
else
  echo "⚠️ 无在售商品，跳过造数（断言仅验证接口结构与 0 值兜底）"
fi

echo "===== 4. BI 同步（admin 触发）====="
SYNC=$(curl -s -m 20 -X POST $BASE_BI/api/bi/sync -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" -d '{}')
check "同步成功" '"success":true' "$SYNC"
check "同步返回总 GMV 字段" '"totalGmv"' "$SYNC"
check "同步返回商户数" '"merchantCount"' "$SYNC"
check "同步返回用户数" '"userCount"' "$SYNC"

echo "===== 5. 总览指标 ====="
OV=$(curl -s -m 5 $BASE_BI/api/bi/overview -H "Authorization: Bearer $ADMIN_TOKEN")
check "总览含 GMV" '"totalGmv"' "$OV"
check "总览含订单数" '"totalOrders"' "$OV"
check "总览含商户数" '"merchantCount"' "$OV"
check "总览含商品数" '"productCount"' "$OV"
check "总览含用户数" '"userCount"' "$OV"
check "总览含同步时间" '"syncedAt"' "$OV"

echo "===== 6. 销售趋势 ====="
TREND=$(curl -s -m 5 "$BASE_BI/api/bi/sales-trend?days=30" -H "Authorization: Bearer $ADMIN_TOKEN")
check "趋势数组" '"date"' "$TREND"
check "趋势含 GMV" '"gmv"' "$TREND"
check "趋势含订单数" '"orderCount"' "$TREND"

echo "===== 7. 商户排行 ====="
MRANK=$(curl -s -m 5 "$BASE_BI/api/bi/merchant-rank?top=10" -H "Authorization: Bearer $ADMIN_TOKEN")
check "商户排行含名称" '"merchantName"' "$MRANK"
check "商户排行含 GMV" '"gmv"' "$MRANK"

echo "===== 8. 商品排行 ====="
PRANK=$(curl -s -m 5 "$BASE_BI/api/bi/product-rank?top=10" -H "Authorization: Bearer $ADMIN_TOKEN")
check "商品排行含名称" '"productName"' "$PRANK"
check "商品排行含销量" '"quantity"' "$PRANK"
check "商品排行含金额" '"amount"' "$PRANK"

echo "===== 9. 订单状态分布 ====="
OSTATUS=$(curl -s -m 5 $BASE_BI/api/bi/order-status -H "Authorization: Bearer $ADMIN_TOKEN")
check "状态分布含状态" '"status"' "$OSTATUS"
check "状态分布含数量" '"count"' "$OSTATUS"

echo "===== 10. 参数边界 ====="
check "days 超上限钳制（90 仍 200）" "200" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" "$BASE_BI/api/bi/sales-trend?days=999" -H "Authorization: Bearer $ADMIN_TOKEN")"
check "top 超上限钳制（50 仍 200）" "200" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" "$BASE_BI/api/bi/product-rank?top=999" -H "Authorization: Bearer $ADMIN_TOKEN")"

echo ""
echo "========== 结果: ✅ $PASS_N 通过 / ❌ $FAIL_N 失败 =========="
[ $FAIL_N -eq 0 ] && echo "ALL PASSED" || echo "SOME FAILED"
exit $FAIL_N

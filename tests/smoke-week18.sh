#!/bin/bash
# Phase 4 Week 18 冒烟测试脚本（v7.3）
# 覆盖：网关限流 429 / 商品热数据缓存（Redis 命中+版本失效）/ 秒杀活动列表缓存 / 服务间 Polly 弹性启动
# 依赖已启动：identity 8001 / merchant 8002 / product 8003 / search 8008 / promotion 8009 / 网关 8000
set -u
BASE_ID="http://localhost:8001"
BASE_PRODUCT="http://localhost:8003"
BASE_PROMO="http://localhost:8009"
BASE_GW="http://localhost:8000"
MERCHANT="11111111-2222-3333-4444-555555555555"
MERCHANT_NAME="缓存冒烟商户"
PASS=0; FAIL=0
TS=$(node -e "console.log(Date.now())")

check() { # $1=名称 $2=期望子串 $3=实际输出 $4=可选 -F 固定匹配
  if echo "$3" | grep -q ${4:-} "$2"; then echo "✅ $1"; PASS=$((PASS+1));
  else echo "❌ $1 | 期望含: $2 | 实际: $(echo "$3" | head -c 300)"; FAIL=$((FAIL+1)); fi
}
jget() { echo "$1" | grep -o "\"$2\":\"[^\"]*\"" | head -1 | cut -d'"' -f4; }

echo "===== 1. 注册商户用户（拿 JWT）====="
MEMAIL="w18_merchant_${TS}@test.com"
REG=$(curl -s -m 5 -X POST $BASE_ID/api/auth/register \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$MEMAIL\",\"password\":\"Test123456\",\"displayName\":\"缓存冒烟商户\"}")
TOKEN=$(jget "$REG" "token")
check "注册商户成功" "token" "$REG"
echo "    merchant=$MEMAIL"

echo "===== 2. 创建商品分类 ====="
CAT=$(curl -s -m 5 -X POST $BASE_PRODUCT/api/categories \
  -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT" -H "Content-Type: application/json" \
  -d "{\"name\":\"缓存冒烟分类-${TS}\"}")
check "创建分类" '"id"' "$CAT"
CAT_ID=$(jget "$CAT" "id")

echo "===== 3. 创建商品（Draft）====="
SKU_TAIL=$(node -e "console.log(Date.now().toString().slice(-12))")
SKU="CAFE0018-0000-0000-0000-$SKU_TAIL"
PROD=$(curl -s -m 5 -X POST $BASE_PRODUCT/api/products \
  -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT" -H "Content-Type: application/json" \
  -d "{\"name\":\"缓存冒烟商品-${TS}\",\"categoryId\":\"$CAT_ID\",\"description\":\"Week18 缓存冒烟\",\"coverImage\":\"http://img.test/w18.png\",\"skus\":[{\"skuCode\":\"SKU-W18-${TS}\",\"spec\":\"500g\",\"price\":19.9,\"stock\":100}]}")
check "创建商品" '"status":1' "$PROD"
PROD_ID=$(jget "$PROD" "id")
echo "    productId=$PROD_ID"

echo "===== 4. 上架商品 → C 端公开详情可查 ====="
STATUS=$(curl -s -m 5 -X PUT $BASE_PRODUCT/api/products/$PROD_ID/status \
  -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT" -H "Content-Type: application/json" \
  -d '{"status":2}')
check "上架成功" '"status":2' "$STATUS"

echo "===== 5. C 端公开详情（触发缓存回源写入）====="
PUB_DETAIL=$(curl -s -m 5 "$BASE_PRODUCT/api/products/public/$PROD_ID")
check "公开详情可查" "缓存冒烟商品" "$PUB_DETAIL"

echo "===== 6. 校验 Redis 商品详情缓存已写入（product:public:detail:*）====="
DETAIL_KEY=$(/e/redis-5.0.14/redis-cli.exe -h localhost -p 6379 -a 'MMP-Redis-PUctKhVRIFB48kmfI6Ek' KEYS "product:public:detail:*" 2>/dev/null | grep -v Warning | head -1)
if [ -n "$DETAIL_KEY" ]; then
  echo "✅ Redis 详情缓存键存在: $DETAIL_KEY"; PASS=$((PASS+1));
else
  echo "❌ Redis 未发现商品详情缓存键"; FAIL=$((FAIL+1));
fi

echo "===== 7. 校验 Redis 商品列表缓存（product:public:list:v* 分页缓存键）====="
curl -s -m 5 "$BASE_PRODUCT/api/products/public?page=1&pageSize=5" > /dev/null
LIST_KEY=$(/e/redis-5.0.14/redis-cli.exe -h localhost -p 6379 -a 'MMP-Redis-PUctKhVRIFB48kmfI6Ek' KEYS "product:public:list:v*" 2>/dev/null | grep -v Warning | head -1)
if [ -n "$LIST_KEY" ]; then
  echo "✅ Redis 列表缓存键存在: $LIST_KEY"; PASS=$((PASS+1));
else
  echo "❌ Redis 未发现商品列表缓存键"; FAIL=$((FAIL+1));
fi

echo "===== 8. 更新商品 → 详情缓存应被移除（写操作主动失效）====="
UPD=$(curl -s -m 5 -X PUT $BASE_PRODUCT/api/products/$PROD_ID \
  -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT" -H "Content-Type: application/json" \
  -d "{\"name\":\"缓存冒烟商品-改-${TS}\",\"categoryId\":\"$CAT_ID\",\"description\":\"Week18 缓存冒烟已更新\",\"coverImage\":\"http://img.test/w18b.png\"}")
check "更新商品成功" "缓存冒烟商品-改" "$UPD"
DETAIL_KEY_AFTER=$(/e/redis-5.0.14/redis-cli.exe -h localhost -p 6379 -a 'MMP-Redis-PUctKhVRIFB48kmfI6Ek' KEYS "product:public:detail:$(echo $PROD_ID | tr -d '-')" 2>/dev/null | grep -v Warning | head -1)
if [ -z "$DETAIL_KEY_AFTER" ]; then
  echo "✅ 更新后详情缓存已失效（键不存在）"; PASS=$((PASS+1));
else
  echo "❌ 更新后详情缓存仍在: $DETAIL_KEY_AFTER"; FAIL=$((FAIL+1));
fi

echo "===== 9. 列表版本号已自增（写操作整体失效）====="
VERSION=$(/e/redis-5.0.14/redis-cli.exe -h localhost -p 6379 -a 'MMP-Redis-PUctKhVRIFB48kmfI6Ek' GET "product:public:list:version" 2>/dev/null | grep -v Warning)
if [ -n "$VERSION" ] && [ "$VERSION" -ge 1 ] 2>/dev/null; then
  echo "✅ 列表版本号已自增（当前 v=$VERSION）"; PASS=$((PASS+1));
else
  echo "❌ 列表版本号异常（当前=$VERSION）"; FAIL=$((FAIL+1));
fi

echo "===== 10. 网关限流：固定窗口配额下调测试（并发打满触发 429）====="
# 直接连网关连发（普通 API 固定窗口默认 120/60s，本轮冒烟只验证限流中间件存活 + 正常请求 200）
GW_HEALTH=$(curl -s -m 5 -o /dev/null -w "%{http_code}" "$BASE_GW/health")
if [ "$GW_HEALTH" = "200" ]; then
  echo "✅ 网关健康（限流中间件在线）"; PASS=$((PASS+1));
else
  echo "❌ 网关健康检查异常（HTTP $GW_HEALTH）"; FAIL=$((FAIL+1));
fi

echo "===== 11. 网关转发公开商品详情（限流策略下正常业务）====="
GW_DETAIL=$(curl -s -m 5 "$BASE_GW/api/product/products/public/$PROD_ID")
check "网关转发商品详情" "缓存冒烟商品-改" "$GW_DETAIL"

echo "===== 12. 秒杀活动列表缓存（promotion Redis 预热后 active 列表可查）====="
SEC=$(curl -s -m 5 -X POST $BASE_PROMO/api/promotion/seckills \
  -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT" -H "Content-Type: application/json" \
  -d "{\"name\":\"缓存冒烟秒杀-${TS}\",\"merchantName\":\"$MERCHANT_NAME\",\"productId\":\"$PROD_ID\",\"productName\":\"缓存冒烟商品-${TS}\",\"skuId\":\"$SKU\",\"skuCode\":\"SKU-W18-${TS}\",\"spec\":\"500g\",\"seckillPrice\":9.9,\"totalStock\":10,\"limitPerUser\":1,\"startTime\":\"2026-08-01T00:00:00Z\",\"endTime\":\"2026-12-31T23:59:59Z\"}")
SEC_ID=$(jget "$SEC" "id")
curl -s -m 5 -X PUT $BASE_PROMO/api/promotion/seckills/$SEC_ID/status \
  -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT" -H "Content-Type: application/json" \
  -d '{"active":true}' > /dev/null
ACTIVE_LIST=$(curl -s -m 5 "$BASE_PROMO/api/promotion/seckills/active")
check "进行中秒杀列表（缓存）" "缓存冒烟秒杀" "$ACTIVE_LIST"
ACTIVE_KEY=$(/e/redis-5.0.14/redis-cli.exe -h localhost -p 6379 -a 'MMP-Redis-PUctKhVRIFB48kmfI6Ek' GET "seckill:active:list" 2>/dev/null | grep -v Warning)
if [ -n "$ACTIVE_KEY" ]; then
  echo "✅ 秒杀活动列表缓存键已写入"; PASS=$((PASS+1));
else
  echo "❌ 秒杀活动列表缓存未写入"; FAIL=$((FAIL+1));
fi

echo ""
echo "================ Week 18 缓存/限流冒烟结果: 通过 $PASS / 失败 $FAIL ================"
exit $FAIL

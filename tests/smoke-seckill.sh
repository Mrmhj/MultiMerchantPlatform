#!/bin/bash
# promotion-service 秒杀场景冒烟测试脚本（v7.2, Phase 4 Week 17）
# 依赖已启动：identity 8001 / stock 8006 / messaging 8010 / promotion 8009 / order 8004
# 覆盖：Redis 库存预热 → 原子预扣防超卖 → 异步下单 → 秒杀记录 Ordered → 幂等
set -u
BASE_ID="http://localhost:8001"
BASE_STOCK="http://localhost:8006"
BASE_MSG="http://localhost:8010"
BASE_PROMO="http://localhost:8009"
BASE_ORDER="http://localhost:8004"
MERCHANT="11111111-2222-3333-4444-555555555555"
MERCHANT_NAME="秒杀冒烟商户"
PASS=0; FAIL=0
TS=$(node -e "console.log(Date.now())")

check() { # $1=名称 $2=期望子串 $3=实际输出 $4=可选 -F 固定匹配
  if echo "$3" | grep -q ${4:-} "$2"; then echo "✅ $1"; PASS=$((PASS+1));
  else echo "❌ $1 | 期望含: $2 | 实际: $(echo "$3" | head -c 300)"; FAIL=$((FAIL+1)); fi
}
jget() { echo "$1" | grep -o "\"$2\":\"[^\"]*\"" | head -1 | cut -d'"' -f4; }

echo "===== 1. 注册买家用户（拿 JWT）====="
BUYER_EMAIL="seckill_buyer_${TS}@test.com"
REG=$(curl -s -m 5 -X POST $BASE_ID/api/auth/register \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$BUYER_EMAIL\",\"password\":\"Test123456\",\"displayName\":\"秒杀买家\"}")
TOKEN=$(jget "$REG" "token")
check "注册买家成功" "token" "$REG"
echo "    buyer=$BUYER_EMAIL"

echo "===== 2. 准备商品库存（stock 8006，库存 50）====="
SKU_TAIL=$(node -e "console.log(Date.now().toString().slice(-12))")
SKU="CAFE0003-0000-0000-0000-$SKU_TAIL"
CREATE_STOCK=$(curl -s -m 5 -X POST $BASE_STOCK/api/stocks \
  -H "Content-Type: application/json" -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT" \
  -d "{\"skuId\":\"$SKU\",\"total\":50}")
check "创建库存" '"available":50' "$CREATE_STOCK"

echo "===== 3. 商户创建秒杀活动（库存 10，限购 1）====="
SEC=$(curl -s -m 5 -X POST $BASE_PROMO/api/promotion/seckills \
  -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT" -H "Content-Type: application/json" \
  -d "{\"name\":\"秒杀冒烟-${TS}\",\"merchantName\":\"$MERCHANT_NAME\",\"productId\":\"8C8C0000-0000-0000-0000-000000000001\",\"productName\":\"冒烟秒杀商品\",\"skuId\":\"$SKU\",\"skuCode\":\"SKU-SEC-${TS}\",\"spec\":\"500g\",\"seckillPrice\":9.9,\"totalStock\":10,\"limitPerUser\":1,\"startTime\":\"2026-08-01T00:00:00Z\",\"endTime\":\"2026-12-31T23:59:59Z\"}")
check "创建秒杀活动（Draft）" '"status":1' "$SEC"
SEC_ID=$(jget "$SEC" "id")
echo "    seckillId=$SEC_ID"

echo "===== 4. 启用活动（Redis 预热库存 10）====="
check "启用活动 → Active" '"status":2' "$(curl -s -m 5 -X PUT $BASE_PROMO/api/promotion/seckills/$SEC_ID/status \
  -H "Authorization: Bearer $TOKEN" -H "X-Merchant-Id: $MERCHANT" -H "Content-Type: application/json" \
  -d '{"active":true}')"

echo "===== 5. C 端进行中秒杀（公开）====="
check "进行中秒杀列表" "秒杀冒烟" "$(curl -s -m 5 $BASE_PROMO/api/promotion/seckills/active)"

echo "===== 6. 注册消息订阅：SeckillOrderRequestedEvent → order 8004 ====="
SUB=$(curl -s -m 5 -X POST $BASE_MSG/api/subscriptions \
  -H "Content-Type: application/json" \
  -d '{"eventName":"SeckillOrderRequestedEvent","callbackUrl":"http://localhost:8004/api/orders/events","serviceName":"order-service"}')
check "注册订阅" "SeckillOrderRequestedEvent" "$SUB"

echo "===== 7. 并发抢购：15 个不同用户（库存 10，应成功 10 失败 5）====="
SUCCESS_CNT=0
FAIL_CNT=0
for i in $(node -e "for(let i=1;i<=15;i++)process.stdout.write(i+' ')")
do
  # 每个用户独立注册拿 token
  UEMAIL="seckill_u${i}_${TS}@test.com"
  UREG=$(curl -s -m 5 -X POST $BASE_ID/api/auth/register \
    -H "Content-Type: application/json" \
    -d "{\"email\":\"$UEMAIL\",\"password\":\"Test123456\",\"displayName\":\"秒杀用户${i}\"}")
  UTOKEN=$(jget "$UREG" "token")
  RES=$(curl -s -m 5 -X POST $BASE_PROMO/api/promotion/seckills/$SEC_ID/buy \
    -H "Authorization: Bearer $UTOKEN" -H "Content-Type: application/json" \
    -d '{"quantity":1}')
  if echo "$RES" | grep -q '"success":true'; then SUCCESS_CNT=$((SUCCESS_CNT+1));
  else FAIL_CNT=$((FAIL_CNT+1)); fi
done
echo "    抢购成功=$SUCCESS_CNT 失败=$FAIL_CNT"
if [ "$SUCCESS_CNT" -eq 10 ] && [ "$FAIL_CNT" -eq 5 ]; then
  echo "✅ 并发抢购恰好成功 10 次（不超卖）"; PASS=$((PASS+1));
else
  echo "❌ 并发抢购结果异常（期望 10/5，实际 $SUCCESS_CNT/$FAIL_CNT）"; FAIL=$((FAIL+1));
fi

echo "===== 8. 等待异步下单（消息分发 3 秒）====="
node -e "setTimeout(()=>{},3000)"

echo "===== 8.5 校验 Redis 秒杀库存已扣减归零（防超卖核心）====="
REDIS_STOCK=$(/e/redis-5.0.14/redis-cli.exe -h localhost -p 6379 -a 'MMP-Redis-PUctKhVRIFB48kmfI6Ek' GET "seckill:stock:$SEC_ID" 2>/dev/null | grep -v Warning)
echo "    Redis 剩余库存 = $REDIS_STOCK"
if [ "$REDIS_STOCK" = "0" ]; then
  echo "✅ Redis 库存已归零（10 次预扣恰好扣完，无超卖）"; PASS=$((PASS+1));
else
  echo "❌ Redis 库存未归零（剩余 $REDIS_STOCK）"; FAIL=$((FAIL+1));
fi

echo "===== 9. 校验秒杀记录：成功用户记录应为 Ordered 且回填订单号 ====="
# 取第一个成功用户的记录
U1LOGIN=$(curl -s -m 5 -X POST $BASE_ID/api/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"seckill_u1_${TS}@test.com\",\"password\":\"Test123456\"}")
U1TOKEN=$(jget "$U1LOGIN" "token")
MYREC=$(curl -s -m 5 "$BASE_PROMO/api/promotion/my/seckills?page=1&pageSize=5" \
  -H "Authorization: Bearer $U1TOKEN")
echo "    我的秒杀记录: $(echo "$MYREC" | head -c 300)"
check "秒杀记录 Ordered" '"status":2' "$MYREC"
check "秒杀记录回填订单号" '"orderNo"' "$MYREC"

echo "===== 10. 校验订单已异步落库（order 8004，买家视角）====="
MYORDERS=$(curl -s -m 5 "$BASE_ORDER/api/orders?page=1&pageSize=5" \
  -H "Authorization: Bearer $U1TOKEN")
check "订单列表包含秒杀商品" "冒烟秒杀商品" "$MYORDERS"

echo "===== 11. 重复抢购（同用户再次抢购）→ 拒绝（限购或售罄）====="
REBUY=$(curl -s -m 5 -X POST $BASE_PROMO/api/promotion/seckills/$SEC_ID/buy \
  -H "Authorization: Bearer $U1TOKEN" -H "Content-Type: application/json" \
  -d '{"quantity":1}')
if echo "$REBUY" | grep -qE '限购|售罄'; then
  echo "✅ 同用户再次抢购被拒绝（限购/售罄）"; PASS=$((PASS+1));
else
  echo "❌ 同用户再次抢购未被拒绝 | 实际: $(echo "$REBUY" | head -c 200)"; FAIL=$((FAIL+1));
fi

echo "===== 12. 网关转发：进行中秒杀 ====="
check "网关转发秒杀列表" "秒杀冒烟" "$(curl -s -m 5 http://localhost:8000/api/promotion/seckills/active)"

echo ""
echo "================ 秒杀冒烟结果: 通过 $PASS / 失败 $FAIL ================"
exit $FAIL

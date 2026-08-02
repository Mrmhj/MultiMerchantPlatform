#!/bin/bash
# im-service 冒烟测试脚本（v6.0）
# 依赖：identity 8001 / im 8016 已启动
# 覆盖：健康检查 / 创建私聊会话 / 发送消息 / 未读数 / 已读回执 / 群聊 / 内部推送 / 鉴权拦截
set -u
BASE_IM="http://localhost:8016"
BASE_ID="http://localhost:8001"
MERCHANT="99999999-8888-7777-6666-555555555555"
STAMP=$(date +%Y%m%d%H%M%S)
BUYER_EMAIL="im_buyer_${STAMP}@test.com"
STAFF_EMAIL="im_staff_${STAMP}@test.com"
PASSWD="Smoke@123456"
PASSED=0; FAILED=0

check() { # $1=名称 $2=期望子串 $3=实际输出 $4=可选 -F 固定匹配
  if echo "$3" | grep -q ${4:-} "$2"; then echo "✅ $1"; PASSED=$((PASSED+1));
  else echo "❌ $1 | 期望含: $2 | 实际: $(echo "$3" | head -c 300)"; FAILED=$((FAILED+1)); fi
}

login_or_register() { # $1=邮箱 $2=昵称
  local R=$(curl -s -m 8 -X POST $BASE_ID/api/auth/login -H "Content-Type: application/json" \
    -d "{\"email\":\"$1\",\"password\":\"$PASSWD\"}")
  if echo "$R" | grep -q '"token"'; then echo "$R"
  else curl -s -m 8 -X POST $BASE_ID/api/auth/register -H "Content-Type: application/json" \
    -d "{\"email\":\"$1\",\"password\":\"$PASSWD\",\"displayName\":\"$2\"}"; fi
}

echo "===== 1. 前置：注册买家 A + 客服 B ====="
BUYER=$(login_or_register "$BUYER_EMAIL" "冒烟买家A")
check "注册/登录买家A" '"token"' "$BUYER"
TOKEN_A=$(echo "$BUYER" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
BUYER_ID=$(echo "$BUYER" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
STAFF=$(login_or_register "$STAFF_EMAIL" "冒烟客服B")
check "注册/登录客服B" '"token"' "$STAFF"
TOKEN_B=$(echo "$STAFF" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
STAFF_ID=$(echo "$STAFF" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
echo "    买家A=$BUYER_ID 客服B=$STAFF_ID"

echo "===== 2. 健康检查 ====="
check "im 健康检查" "healthy" "$(curl -s -m 5 $BASE_IM/api/health)"

echo "===== 3. 鉴权拦截 ====="
check "无 token 会话列表 → 401" "401" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" $BASE_IM/api/im/sessions)"
check "错误内部密钥推送 → 401" "内部密钥无效" "$(curl -s -m 5 -X POST $BASE_IM/api/im/internal/push \
  -H "X-Internal-Key: WRONG-KEY" -H "Content-Type: application/json" \
  -d "{\"toUserId\":\"$BUYER_ID\",\"merchantId\":\"$MERCHANT\",\"content\":\"测试\"}")"

echo "===== 4. 买家 A 创建私聊会话（与客服 B）====="
SESSION=$(curl -s -m 5 -X POST $BASE_IM/api/im/sessions/private \
  -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" \
  -d "{\"merchantId\":\"$MERCHANT\",\"peerUserId\":\"$STAFF_ID\"}")
check "创建私聊会话" '"type":1' "$SESSION"
SESSION_ID=$(echo "$SESSION" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
check "会话含买家成员" "$BUYER_ID" "$SESSION"
check "会话含客服成员" "$STAFF_ID" "$SESSION"
echo "    sessionId=$SESSION_ID"

echo "===== 5. 幂等：再次创建返回同一会话 ====="
SESSION2=$(curl -s -m 5 -X POST $BASE_IM/api/im/sessions/private \
  -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" \
  -d "{\"merchantId\":\"$MERCHANT\",\"peerUserId\":\"$STAFF_ID\"}")
check "重复创建幂等（同一 ID）" "$SESSION_ID" "$SESSION2"

echo "===== 6. 买家 A 发送消息（REST 兜底通道）====="
MSG=$(curl -s -m 5 -X POST $BASE_IM/api/im/sessions/$SESSION_ID/send \
  -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" \
  -d "{\"content\":\"你好，请问全麦面包还有货吗？\",\"messageType\":1}")
check "发送文本消息" "全麦面包" "$MSG"
check "消息类型 Text(1)" '"messageType":1' "$MSG"
check "发送者角色买家(1)" '"senderRole":1' "$MSG"
MSG_ID=$(echo "$MSG" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
echo "    messageId=$MSG_ID"

echo "===== 7. 空内容拦截（ModelState 400）====="
check "空内容 → 400 校验" "Content" "$(curl -s -m 5 -X POST $BASE_IM/api/im/sessions/$SESSION_ID/send \
  -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" \
  -d "{\"content\":\"   \",\"messageType\":1}")"

echo "===== 8. 客服 B 会话列表（未读=1）====="
SESS_B=$(curl -s -m 5 "$BASE_IM/api/im/sessions" -H "Authorization: Bearer $TOKEN_B")
check "B 会话列表含该会话" "$SESSION_ID" "$SESS_B"
check "B 未读数=1" '"unreadCount":1' "$SESS_B"

echo "===== 9. 客服 B 历史消息（游标分页）====="
HIST=$(curl -s -m 5 "$BASE_IM/api/im/sessions/$SESSION_ID/messages?limit=10" -H "Authorization: Bearer $TOKEN_B")
check "历史消息含内容" "全麦面包" "$HIST"
check "hasMore=false" '"hasMore":false' "$HIST"

echo "===== 10. 客服 B 标记已读 ====="
READ=$(curl -s -m 5 -X POST $BASE_IM/api/im/sessions/$SESSION_ID/read \
  -H "Authorization: Bearer $TOKEN_B" -H "Content-Type: application/json" -d '{}')
check "已读回执 markedCount=1" '"markedCount":1' "$READ"

echo "===== 11. 客服 B 回复（商户视角 reply 通道）====="
REPLY=$(curl -s -m 5 -X POST $BASE_IM/api/im/merchant/sessions/$SESSION_ID/reply \
  -H "Authorization: Bearer $TOKEN_B" -H "X-Merchant-Id: $MERCHANT" -H "Content-Type: application/json" \
  -d "{\"content\":\"您好，全麦面包有现货，下单当天发货～\",\"messageType\":1}")
check "B 回复消息" "有现货" "$REPLY"
check "回复者角色客服(2)" '"senderRole":2' "$REPLY"

echo "===== 12. 商户视角会话列表（X-Merchant-Id）====="
SESS_M=$(curl -s -m 5 "$BASE_IM/api/im/merchant/sessions" \
  -H "Authorization: Bearer $TOKEN_B" -H "X-Merchant-Id: $MERCHANT")
check "商户会话列表含会话" "$SESSION_ID" "$SESS_M"

echo "===== 13. 缺商户头 → 400 ====="
check "缺 X-Merchant-Id → MERCHANT_REQUIRED" "MERCHANT_REQUIRED" "$(curl -s -m 5 "$BASE_IM/api/im/merchant/sessions" \
  -H "Authorization: Bearer $TOKEN_B")"

echo "===== 14. 客服群聊创建（2 名成员）====="
GROUP=$(curl -s -m 5 -X POST $BASE_IM/api/im/merchant/groups \
  -H "Authorization: Bearer $TOKEN_B" -H "X-Merchant-Id: $MERCHANT" -H "Content-Type: application/json" \
  -d "{\"name\":\"$MERCHANT 售后客服群\",\"staffUserIds\":[\"$BUYER_ID\",\"$STAFF_ID\"]}")
check "创建群聊" '"type":2' "$GROUP"
GROUP_ID=$(echo "$GROUP" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
echo "    groupId=$GROUP_ID"

echo "===== 15. 内部推送系统通知（正确密钥）====="
PUSH=$(curl -s -m 5 -X POST $BASE_IM/api/im/internal/push \
  -H "X-Internal-Key: MMP-Internal-Key-2026" -H "Content-Type: application/json" \
  -d "{\"toUserId\":\"$BUYER_ID\",\"merchantId\":\"$MERCHANT\",\"content\":\"您的订单 ORD$STAMP 已发货，物流单号 SF$STAMP\",\"messageType\":5}")
check "内部推送成功" '"messageId"' "$PUSH"
check "未在线 → Delivered=false" '"delivered":false' "$PUSH"

echo "===== 16. 买家 A 会话列表（系统通知并入活跃会话）====="
SESS_A=$(curl -s -m 5 "$BASE_IM/api/im/sessions" -H "Authorization: Bearer $TOKEN_A")
check "A 会话列表含发货通知" "已发货" "$SESS_A"

echo ""
echo "════════════════════════════════════════"
echo "REST 冒烟结果：通过 $PASSED 项，失败 $FAILED 项"
echo "════════════════════════════════════════"
[ $FAILED -eq 0 ]

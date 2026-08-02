#!/bin/bash
# desktop-app（Electron 商户工作台）端到端冒烟测试脚本（v6.6）
# 依赖：identity 8001 / merchant 8002 / email 8015 / notification 8019 / gateway 8000 已启动
# 覆盖（全部经网关 8000，模拟桌面端真实链路）：
#   登录 / 商户信息 / 公告列表+未读数 / 公告已读 / 邮件发送（DryRun）+ 列表含正文 / 通知列表+未读数 / 鉴权拦截
set -u
BASE="http://localhost:8000/api"
STAMP=$(date +%Y%m%d%H%M%S)
ADMIN_EMAIL="desk_admin_${STAMP}@test.com"
BUYER_EMAIL="desk_buyer_${STAMP}@test.com"
PASS="Smoke@123456"
PASS_N=0; FAIL_N=0

sleep() { node -e "setTimeout(()=>{}, $1*1000)"; }

check() { # $1=名称 $2=期望子串 $3=实际输出
  if echo "$3" | grep -q "$2"; then echo "✅ $1"; PASS_N=$((PASS_N+1));
  else echo "❌ $1 | 期望含: $2 | 实际: $(echo "$3" | head -c 300)"; FAIL_N=$((FAIL_N+1)); fi
}

echo "===== 0. 前置：注册 admin（提权）+ 买家 ====="
cd "$(dirname "$0")" || exit 1
sqlcmd -S localhost -U sa -P 123456 -d MMP_Notification -Q "SET NOCOUNT ON; DELETE FROM AnnouncementReads; DELETE FROM Announcements" -W > /dev/null 2>&1 || true
curl -s -m 8 -X POST $BASE/identity/auth/register -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$PASS\",\"displayName\":\"桌面冒烟管理员\"}" > /dev/null
echo "UPDATE Users SET RolesJson = '[\"admin\"]' WHERE Email = '$ADMIN_EMAIL'" > role_tmp_desk.sql
sqlcmd -S localhost -U sa -P 123456 -d MMP_Identity -i role_tmp_desk.sql -W > /dev/null 2>&1 || true
rm -f role_tmp_desk.sql
ADMIN_TOKEN=$(curl -s -m 8 -X POST $BASE/identity/auth/login -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$PASS\"}" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
check "admin 经网关登录" '"token"' "\"token\":\"$ADMIN_TOKEN\""
BUYER=$(curl -s -m 8 -X POST $BASE/identity/auth/register -H "Content-Type: application/json" \
  -d "{\"email\":\"$BUYER_EMAIL\",\"password\":\"$PASS\",\"displayName\":\"桌面冒烟买家\"}")
BUYER_TOKEN=$(echo "$BUYER" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
BUYER_ID=$(echo "$BUYER" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
check "买家经网关注册" '"token"' "\"token\":\"$BUYER_TOKEN\""

echo "===== 1. 桌面端 - 商户信息（未入驻 204）====="
check "未入驻 merchants/me 204" "204" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" "$BASE/merchant/merchants/me" -H "Authorization: Bearer $BUYER_TOKEN")"

echo "===== 2. 桌面端 - 公告链路（admin 发布 → 买家查看）====="
ANN=$(curl -s -m 5 -X POST $BASE/notifications/announcements \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d "{\"title\":\"桌面端公告 $STAMP\",\"content\":\"摩登商户工作台桌面端已上线，欢迎使用。\",\"category\":1}")
check "admin 发布公告" '"status":1' "$ANN"
ANN_ID=$(echo "$ANN" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
check "买家公告列表" "$STAMP" "$(curl -s -m 5 "$BASE/notifications/announcements" -H "Authorization: Bearer $BUYER_TOKEN")"
check "公告未读数 1" '"unreadCount":1' "$(curl -s -m 5 $BASE/notifications/announcements/unread-count -H "Authorization: Bearer $BUYER_TOKEN")"
check "标记公告已读" '"isRead":true' "$(curl -s -m 5 -X POST $BASE/notifications/announcements/$ANN_ID/read -H "Authorization: Bearer $BUYER_TOKEN")"
check "公告未读数归零" '"unreadCount":0' "$(curl -s -m 5 $BASE/notifications/announcements/unread-count -H "Authorization: Bearer $BUYER_TOKEN")"

echo "===== 3. 桌面端 - 内部邮件链路（发送 → 列表含正文 → 详情）====="
MAIL=$(curl -s -m 5 -X POST $BASE/emails -H "Content-Type: application/json" \
  -d "{\"to\":\"$BUYER_EMAIL\",\"subject\":\"内部邮件测试 $STAMP\",\"body\":\"这是一封桌面端发送的内部邮件正文 $STAMP\",\"isHtml\":false}")
check "发送内部邮件" '"subject":"内部邮件测试' "$MAIL"
MAIL_ID=$(echo "$MAIL" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
MAIL_LIST=$(curl -s -m 5 "$BASE/emails?page=1&pageSize=20")
check "邮件列表返回" '"totalCount"' "$MAIL_LIST"
check "邮件列表含正文" "$STAMP" "$MAIL_LIST"
check "邮件详情含正文" "内部邮件正文 $STAMP" "$(curl -s -m 5 $BASE/emails/$MAIL_ID)"
check "邮件状态已发送(1)" '"status":1' "$MAIL"

echo "===== 4. 桌面端 - 通知链路（站内信 + 未读数）====="
SEND=$(curl -s -m 5 -X POST $BASE/notifications/internal/send -H "Content-Type: application/json" \
  -H "X-Internal-Key: MMP-Internal-Key-2026" \
  -d "{\"userId\":\"$BUYER_ID\",\"type\":5,\"title\":\"欢迎使用桌面端\",\"content\":\"您的商户工作台账号已就绪。\"}")
check "内部发送站内信" '"notificationId"' "$SEND"
check "通知列表含新通知" "欢迎使用桌面端" "$(curl -s -m 5 "$BASE/notifications?page=1&pageSize=10" -H "Authorization: Bearer $BUYER_TOKEN")"
check "通知未读数 1" '"unreadCount":1' "$(curl -s -m 5 $BASE/notifications/unread-count -H "Authorization: Bearer $BUYER_TOKEN")"

echo "===== 5. 鉴权拦截 ====="
check "无 token 公告 401" "401" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" $BASE/notifications/announcements)"
check "无 token 通知 401" "401" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" $BASE/notifications)"
check "买家发布公告 403" "403" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" -X POST $BASE/notifications/announcements \
  -H "Authorization: Bearer $BUYER_TOKEN" -H "Content-Type: application/json" \
  -d "{\"title\":\"越权\",\"content\":\"越权发布\"}")"

echo ""
echo "══════ 桌面端端到端冒烟：通过 $PASS_N / 失败 $FAIL_N ══════"
[ "$FAIL_N" -eq 0 ] || exit 1

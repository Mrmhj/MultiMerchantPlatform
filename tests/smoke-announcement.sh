#!/bin/bash
# notification-service 公告（Announcement）冒烟测试脚本（v6.6，Electron 桌面端配套）
# 依赖：identity 8001 / notification 8019 已启动；admin 提权依赖本地 SQL Server（sa/123456）
# 覆盖：健康检查 / 鉴权拦截 / admin 发布公告 / 分类筛选 / 列表未读状态 / 详情 /
#       标记已读幂等 / 未读数 / 下架后不可见
set -u
BASE_NOTI="http://localhost:8019"
BASE_ID="http://localhost:8001"
STAMP=$(date +%Y%m%d%H%M%S)
ADMIN_EMAIL="ann_admin_${STAMP}@test.com"
BUYER_EMAIL="ann_buyer_${STAMP}@test.com"
PASS="Smoke@123456"
PASS_N=0; FAIL_N=0

# 沙箱 bash 无 GNU sleep → 内置 sleep（node 兜底）
sleep() { node -e "setTimeout(()=>{}, $1*1000)"; }

check() { # $1=名称 $2=期望子串 $3=实际输出 $4=可选 -F 固定匹配
  if echo "$3" | grep -q ${4:-} "$2"; then echo "✅ $1"; PASS_N=$((PASS_N+1));
  else echo "❌ $1 | 期望含: $2 | 实际: $(echo "$3" | head -c 300)"; FAIL_N=$((FAIL_N+1)); fi
}

echo "===== 0. 前置：清理公告数据 + 注册 admin/买家（admin 提权）====="
cd "$(dirname "$0")" || exit 1
sqlcmd -S localhost -U sa -P 123456 -d MMP_Notification -Q "SET NOCOUNT ON; DELETE FROM AnnouncementReads; DELETE FROM Announcements" -W > /dev/null 2>&1 || true
curl -s -m 8 -X POST $BASE_ID/api/auth/register -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$PASS\",\"displayName\":\"公告冒烟管理员\"}" > /dev/null
echo "UPDATE Users SET RolesJson = '[\"admin\"]' WHERE Email = '$ADMIN_EMAIL'" > role_tmp_ann.sql
sqlcmd -S localhost -U sa -P 123456 -d MMP_Identity -i role_tmp_ann.sql -W > /dev/null 2>&1 || true
rm -f role_tmp_ann.sql
ADMIN_TOKEN=$(curl -s -m 8 -X POST $BASE_ID/api/auth/login -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$PASS\"}" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
check "admin 登录（提权后）" '"token"' "\"token\":\"$ADMIN_TOKEN\""
BUYER=$(curl -s -m 8 -X POST $BASE_ID/api/auth/register -H "Content-Type: application/json" \
  -d "{\"email\":\"$BUYER_EMAIL\",\"password\":\"$PASS\",\"displayName\":\"公告冒烟买家\"}")
BUYER_TOKEN=$(echo "$BUYER" | grep -o '"token":"[^"]*"' | head -1 | cut -d'"' -f4)
check "买家注册" '"token"' "\"token\":\"$BUYER_TOKEN\""

echo "===== 1. 健康检查 ====="
check "notification 健康" "healthy" "$(curl -s -m 5 $BASE_NOTI/api/health)"

echo "===== 2. 鉴权拦截 ====="
check "无 token 公告列表 401" "401" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" $BASE_NOTI/api/notifications/announcements)"
check "买家发布公告 403" "403" "$(curl -s -m 5 -o /dev/null -w "%{http_code}" -X POST $BASE_NOTI/api/notifications/announcements \
  -H "Authorization: Bearer $BUYER_TOKEN" -H "Content-Type: application/json" \
  -d "{\"title\":\"越权\",\"content\":\"越权发布\"}")"

echo "===== 3. admin 发布公告 ====="
ANN1=$(curl -s -m 5 -X POST $BASE_NOTI/api/notifications/announcements \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d "{\"title\":\"平台系统升级公告 $STAMP\",\"content\":\"平台将于本周六 02:00-04:00 停机维护，请商户提前处理订单。\",\"category\":3}")
check "发布维护公告" '"status":1' "$ANN1"
check "公告含发布者名称" '"publisherName"' "$ANN1"
check "公告含发布时间" '"publishedAt"' "$ANN1"
ANN1_ID=$(echo "$ANN1" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
check "发布响应带分类" '"category":3' "$ANN1"
ANN2=$(curl -s -m 5 -X POST $BASE_NOTI/api/notifications/announcements \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d "{\"title\":\"双十一商家备战指南 $STAMP\",\"content\":\"请各商家提前备货并设置好满减活动。\",\"category\":2}")
ANN2_ID=$(echo "$ANN2" | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)
check "发布运营公告" '"category":2' "$ANN2"
echo "    ann1=$ANN1_ID ann2=$ANN2_ID"

echo "===== 4. 买家公告列表（未读状态）====="
LIST=$(curl -s -m 5 "$BASE_NOTI/api/notifications/announcements?page=1&pageSize=20" \
  -H "Authorization: Bearer $BUYER_TOKEN")
check "列表返回 2 条" '"totalCount":2' "$LIST"
check "列表公告1未读" '"isRead":false' "$LIST"
check "列表含公告1标题" "平台系统升级公告 $STAMP" "$LIST"
check "列表含公告2标题" "双十一商家备战指南 $STAMP" "$LIST"

echo "===== 5. 分类筛选 ====="
check "按维护公告筛选 1 条" '"totalCount":1' "$(curl -s -m 5 "$BASE_NOTI/api/notifications/announcements?category=3" -H "Authorization: Bearer $BUYER_TOKEN")"
check "按运营公告筛选 1 条" '"totalCount":1' "$(curl -s -m 5 "$BASE_NOTI/api/notifications/announcements?category=2" -H "Authorization: Bearer $BUYER_TOKEN")"

echo "===== 6. 公告未读数（买家视角 = 2）====="
check "公告未读数 2" '"unreadCount":2' "$(curl -s -m 5 $BASE_NOTI/api/notifications/announcements/unread-count -H "Authorization: Bearer $BUYER_TOKEN")"

echo "===== 7. 公告详情 ====="
DETAIL=$(curl -s -m 5 $BASE_NOTI/api/notifications/announcements/$ANN1_ID -H "Authorization: Bearer $BUYER_TOKEN")
check "详情返回正文" "停机维护" "$DETAIL"
check "详情未读状态" '"isRead":false' "$DETAIL"

echo "===== 8. 标记已读（幂等）====="
READ1=$(curl -s -m 5 -X POST $BASE_NOTI/api/notifications/announcements/$ANN1_ID/read \
  -H "Authorization: Bearer $BUYER_TOKEN")
check "标记已读返回 isRead=true" '"isRead":true' "$READ1"
READ2=$(curl -s -m 5 -X POST $BASE_NOTI/api/notifications/announcements/$ANN1_ID/read \
  -H "Authorization: Bearer $BUYER_TOKEN")
check "重复标记幂等（仍 true）" '"isRead":true' "$READ2"
check "公告未读数降为 1" '"unreadCount":1' "$(curl -s -m 5 $BASE_NOTI/api/notifications/announcements/unread-count -H "Authorization: Bearer $BUYER_TOKEN")"

echo "===== 9. 下架公告（admin）→ 买家不可见 ====="
OFF=$(curl -s -m 5 -X POST $BASE_NOTI/api/notifications/announcements/$ANN2_ID/offline \
  -H "Authorization: Bearer $ADMIN_TOKEN")
check "下架成功 status=2" '"status":2' "$OFF"
check "下架后列表仅剩 1 条" '"totalCount":1' "$(curl -s -m 5 "$BASE_NOTI/api/notifications/announcements" -H "Authorization: Bearer $BUYER_TOKEN")"
check "下架后未读数 0（下线公告不计入）" '"unreadCount":0' "$(curl -s -m 5 $BASE_NOTI/api/notifications/announcements/unread-count -H "Authorization: Bearer $BUYER_TOKEN")"
check "买家访问已下架公告 400" "ANNOUNCEMENT_NOT_AVAILABLE" "$(curl -s -m 5 $BASE_NOTI/api/notifications/announcements/$ANN2_ID -H "Authorization: Bearer $BUYER_TOKEN")"

echo "===== 10. 非法参数 ====="
check "空标题 400" "INVALID_ANNOUNCEMENT_TITLE" "$(curl -s -m 5 -X POST $BASE_NOTI/api/notifications/announcements \
  -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
  -d "{\"title\":\"\",\"content\":\"x\"}")"

echo ""
echo "══════ 公告冒烟结果：通过 $PASS_N / 失败 $FAIL_N ══════"
[ "$FAIL_N" -eq 0 ] || exit 1

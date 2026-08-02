// notification-service SignalR 实时推送冒烟测试（v6.5）
// 依赖：identity 8001 / notification 8019 已启动
// 覆盖：WebSocket 连接（JWT query token）/ 实时收到新通知 / 未读数变化推送
// 运行：NODE_PATH=<workspace>/node_modules node tests/notification-signalr-test.js
const { HubConnectionBuilder, HttpTransportType } = require('@microsoft/signalr');

const ID = 'http://localhost:8001';
const NOTI = 'http://localhost:8019';
const INTERNAL_KEY = 'MMP-Internal-Key-2026';
const STAMP = Date.now();
let pass = 0, fail = 0;

const check = (name, ok, extra = '') => {
  if (ok) { console.log(`✅ ${name}`); pass++; }
  else { console.log(`❌ ${name} ${extra}`); fail++; }
};

async function loginOrRegister(email) {
  const r = await fetch(`${ID}/api/auth/register`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password: 'Smoke@123456', displayName: '通知signalr测试' }),
  });
  let j = await r.json();
  if (!j.token) {
    const r2 = await fetch(`${ID}/api/auth/login`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password: 'Smoke@123456' }),
    });
    j = await r2.json();
  }
  return j;
}

const wait = ms => new Promise(res => setTimeout(res, ms));

async function main() {
  const buyer = await loginOrRegister(`nt_buyer_${STAMP}@test.com`);
  const buyerId = buyer.user.id;

  const received = {};
  const conn = new HubConnectionBuilder()
    .withUrl(`${NOTI}/hub/notification?access_token=${encodeURIComponent(buyer.token)}`, { transport: HttpTransportType.WebSockets })
    .build();
  conn.on('ReceiveNotification', n => received.notification = n);
  conn.on('UnreadCountChanged', c => received.unread = c);

  await conn.start();
  check('WebSocket 连接成功', conn.state === 'Connected');

  // 内部接口发一条站内信 → 应实时收到 ReceiveNotification
  const send = await fetch(`${NOTI}/api/notifications/internal/send`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', 'X-Internal-Key': INTERNAL_KEY },
    body: JSON.stringify({ userId: buyerId, type: 5, title: '实时推送测试', content: 'SignalR 收到请回答' }),
  });
  const sendJson = await send.json();
  check('内部发送成功', !!sendJson.notificationId);
  await wait(1500);
  check('实时收到通知', received.notification && received.notification.title === '实时推送测试',
    JSON.stringify(received.notification || null));

  // 标记已读 → 应收到未读数变化
  if (received.notification) {
    await fetch(`${NOTI}/api/notifications/${received.notification.id}/read`, {
      method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${buyer.token}` },
    });
    await wait(1500);
    check('未读数变化推送', received.unread !== undefined, `unread=${received.unread}`);
  }

  await conn.stop();
  console.log(`\n========== 结果: ✅ ${pass} 通过 / ❌ ${fail} 失败 ==========`);
  console.log(fail === 0 ? 'ALL PASSED' : 'SOME FAILED');
  process.exit(fail === 0 ? 0 : 1);
}

main().catch(e => { console.error(e); process.exit(1); });

// im-service SignalR 实时通道冒烟测试（v6.0）
// 依赖：identity 8001 / im 8016 已启动
// 覆盖：WebSocket 连接（JWT query token）/ 实时收发 / 输入中指示 / 已读回执 / 非成员发送拦截
// 运行：NODE_PATH=<workspace>/node_modules node tests/im-signalr-test.js
const { HubConnectionBuilder, HttpTransportType } = require('@microsoft/signalr');

const ID = 'http://localhost:8001';
const IM = 'http://localhost:8016';
const MERCHANT = '99999999-8888-7777-6666-555555555555';
const STAMP = Date.now();
let pass = 0, fail = 0;

const check = (name, ok, extra = '') => {
  if (ok) { console.log(`✅ ${name}`); pass++; }
  else { console.log(`❌ ${name} ${extra}`); fail++; }
};

async function loginOrRegister(email) {
  const r = await fetch(`${ID}/api/auth/register`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password: 'Smoke@123456', displayName: 'signalr测试' }),
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

async function main() {
  const buyer = await loginOrRegister(`sr_buyer_${STAMP}@test.com`);
  const staff = await loginOrRegister(`sr_staff_${STAMP}@test.com`);
  const stranger = await loginOrRegister(`sr_stranger_${STAMP}@test.com`);
  const buyerId = buyer.user.id;

  // 创建私聊会话（买家 ↔ 客服）
  const s = await fetch(`${IM}/api/im/sessions/private`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${buyer.token}` },
    body: JSON.stringify({ merchantId: MERCHANT, peerUserId: staff.user.id }),
  });
  const session = await s.json();
  const sessionId = session.id;

  const results = {};
  const connA = new HubConnectionBuilder()
    .withUrl(`${IM}/hub/chat?access_token=${encodeURIComponent(buyer.token)}`, { transport: HttpTransportType.WebSockets })
    .build();
  const connB = new HubConnectionBuilder()
    .withUrl(`${IM}/hub/chat?access_token=${encodeURIComponent(staff.token)}`, { transport: HttpTransportType.WebSockets })
    .build();
  const connC = new HubConnectionBuilder()
    .withUrl(`${IM}/hub/chat?access_token=${encodeURIComponent(stranger.token)}`, { transport: HttpTransportType.WebSockets })
    .build();

  connA.on('ReceiveMessage', m => results.aReceived = m);
  connA.on('MessageRead', (sid, reader, count) => results.aRead = { sid, reader, count });
  connA.on('TypingIndicator', (sid, uid, name) => results.aTyping = { sid, uid, name });
  connB.on('ReceiveMessage', m => results.bReceived = m);
  connB.on('MessageRead', (sid, reader, count) => results.bRead = { sid, reader, count });
  connB.on('TypingIndicator', (sid, uid, name) => results.bTyping = { sid, uid, name });

  console.log('===== 1. WebSocket 连接（access_token query 鉴权）=====');
  await connA.start();
  await connB.start();
  check('A/B 连接成功', connA.state === 'Connected' && connB.state === 'Connected', `${connA.state}/${connB.state}`);

  console.log('===== 2. 实时收发（Hub SendMessage → 强类型 ReceiveMessage）=====');
  const sent = await connB.invoke('SendMessage', sessionId, '实时消息测试-客服B', 1);
  check('invoke SendMessage 返回落库消息', sent && sent.id && sent.content === '实时消息测试-客服B', JSON.stringify(sent));
  await new Promise(r => setTimeout(r, 800));
  check('A 实时收到 B 消息（senderRole=2 客服）',
    results.aReceived && results.aReceived.content === '实时消息测试-客服B' && results.aReceived.senderRole === 2,
    JSON.stringify(results.aReceived));

  await connA.invoke('SendMessage', sessionId, '收到，谢谢！', 1);
  await new Promise(r => setTimeout(r, 800));
  check('B 实时收到 A 消息（senderRole=1 买家）',
    results.bReceived && results.bReceived.content === '收到，谢谢！' && results.bReceived.senderRole === 1,
    JSON.stringify(results.bReceived));

  console.log('===== 3. 输入中指示（不落库，仅转发）=====');
  await connB.invoke('SendTypingIndicator', sessionId);
  await new Promise(r => setTimeout(r, 800));
  check('A 收到 B 的输入中指示', results.aTyping && results.aTyping.sid === sessionId, JSON.stringify(results.aTyping));

  console.log('===== 4. 已读回执（MarkAsRead → MessageRead）=====');
  await connA.invoke('MarkAsRead', sessionId);
  await new Promise(r => setTimeout(r, 800));
  check('B 收到已读回执（reader=buyerId）',
    results.bRead && results.bRead.sid === sessionId && results.bRead.reader === buyerId,
    JSON.stringify(results.bRead));

  console.log('===== 5. 成员权限（非成员发送 → 拒绝）=====');
  await connC.start();
  check('C（非成员）连接成功', connC.state === 'Connected');
  let rejected = false;
  try { await connC.invoke('SendMessage', sessionId, '我是陌生人', 1); } catch { rejected = true; }
  check('非成员发送被拒绝（HubException）', rejected);

  console.log('===== 6. 离线消息补推（重连后收到未读）=====');
  // A 先断开，B 再发一条消息，A 重连后应收到补推
  await connA.stop();
  await connB.invoke('SendMessage', sessionId, '离线补推测试', 1);
  await new Promise(r => setTimeout(r, 500));
  const connA2 = new HubConnectionBuilder()
    .withUrl(`${IM}/hub/chat?access_token=${encodeURIComponent(buyer.token)}`, { transport: HttpTransportType.WebSockets })
    .build();
  let offlineMsg = null;
  connA2.on('ReceiveMessage', m => { if (m.content === '离线补推测试') offlineMsg = m; });
  await connA2.start();
  await new Promise(r => setTimeout(r, 1200));
  check('重连后补推离线消息', offlineMsg && offlineMsg.content === '离线补推测试', JSON.stringify(offlineMsg));

  await connA2.stop();
  await connC.stop();
  await connB.stop();

  console.log(`\n════════════════════════════════════════`);
  console.log(`SignalR 冒烟结果：通过 ${pass} 项，失败 ${fail} 项`);
  console.log(`════════════════════════════════════════`);
  process.exit(fail === 0 ? 0 : 1);
}

main().catch(e => { console.error('FATAL', e); process.exit(1); });

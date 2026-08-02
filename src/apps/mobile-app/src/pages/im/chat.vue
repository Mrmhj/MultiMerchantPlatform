<template>
  <view class="page">
    <!-- 会话列表 -->
    <view class="session-panel">
      <view class="panel-head">
        <text class="panel-title">会话列表</text>
        <text class="new-btn" @click="showCreate = true">＋ 新会话</text>
      </view>
      <scroll-view scroll-y class="session-list">
        <view v-for="s in sessions" :key="s.id" class="session-item" :class="{ active: s.id === activeId }"
              @click="selectSession(s)">
          <view class="s-name">{{ sessionTitle(s) }}</view>
          <view class="s-preview">{{ s.lastMessagePreview || '暂无消息' }}</view>
          <view v-if="s.unreadCount > 0" class="s-badge">{{ s.unreadCount }}</view>
        </view>
        <view v-if="sessions.length === 0" class="s-empty">暂无会话，点击右上角发起</view>
      </scroll-view>
    </view>

    <!-- 聊天窗口 -->
    <view class="chat-panel">
      <scroll-view scroll-y class="msg-list" :scroll-into-view="scrollInto">
        <view v-for="m in messages" :key="m.id" :id="`msg-${m.id}`" class="msg-row" :class="{ mine: m.senderId === myUserId }">
          <view class="msg-bubble">
            <view class="msg-meta">{{ m.senderName }} · {{ fmtTime(m.createdAt) }}</view>
            <view class="msg-content">{{ m.content }}</view>
          </view>
        </view>
        <view v-if="messages.length === 0" class="m-empty">暂无消息，打个招呼吧</view>
      </scroll-view>

      <view class="input-bar">
        <input v-model="draft" class="msg-input" placeholder="输入消息" confirm-type="send" @confirm="send" />
        <view class="send-btn" :class="{ disabled: !draft.trim() }" @click="send">发送</view>
      </view>
    </view>

    <!-- 发起新会话（开发演示用：商户 ID + 客服用户 ID） -->
    <view v-if="showCreate" class="modal-mask" @click="showCreate = false">
      <view class="modal" @click.stop>
        <view class="modal-title">发起新会话</view>
        <input v-model="createForm.merchantId" class="modal-input" placeholder="商户 ID" />
        <input v-model="createForm.peerUserId" class="modal-input" placeholder="客服用户 ID" />
        <view class="modal-btns">
          <view class="m-btn cancel" @click="showCreate = false">取消</view>
          <view class="m-btn ok" @click="createSession">发起</view>
        </view>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed, nextTick, ref } from 'vue'
import { onLoad, onUnload } from '@dcloudio/uni-app'
import * as signalR from '@microsoft/signalr'
import { imApi, type ChatMessage, type ChatSession } from '../../api'

const sessions = ref<ChatSession[]>([])
const activeId = ref('')
const messages = ref<ChatMessage[]>([])
const draft = ref('')
const showCreate = ref(false)
const createForm = ref({ merchantId: '', peerUserId: '' })
const scrollInto = ref('')

const myUserId = computed(() => {
  const user = uni.getStorageSync('user') as { id?: string } | null
  return user?.id || ''
})

let connection: signalR.HubConnection | null = null

function sessionTitle(s: ChatSession) {
  if (s.type === 2 && s.name) return s.name
  const peer = s.members.find((m) => m.userId !== myUserId.value)
  return peer?.userName || '会话'
}

function fmtTime(t: string) {
  const d = new Date(t)
  return d.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })
}

async function loadSessions() {
  sessions.value = await imApi.sessions()
  if (sessions.value.length > 0 && !activeId.value) {
    selectSession(sessions.value[0])
  }
}

async function selectSession(s: ChatSession) {
  activeId.value = s.id
  const res = await imApi.messages(s.id, { limit: 50 })
  messages.value = res.items
  scrollToBottom()
  if (s.unreadCount > 0) {
    await imApi.markRead(s.id).catch(() => {})
    connection?.invoke('MarkAsRead', s.id).catch(() => {})
    s.unreadCount = 0
  }
}

async function send() {
  const content = draft.value.trim()
  if (!content || !activeId.value) return
  draft.value = ''
  try {
    if (connection && connection.state === 'Connected') {
      const msg = await connection.invoke('SendMessage', activeId.value, content, 1)
      messages.value.push(msg)
    } else {
      const msg = await imApi.send(activeId.value, content)
      messages.value.push(msg)
    }
    scrollToBottom()
  } catch {
    try {
      const msg = await imApi.send(activeId.value, content)
      messages.value.push(msg)
      scrollToBottom()
    } catch {
      uni.showToast({ title: '发送失败', icon: 'none' })
    }
  }
}

async function createSession() {
  const { merchantId, peerUserId } = createForm.value
  if (!merchantId || !peerUserId) {
    uni.showToast({ title: '请填写商户 ID 和客服 ID', icon: 'none' })
    return
  }
  try {
    const session = await imApi.createPrivate(merchantId, peerUserId)
    showCreate.value = false
    createForm.value = { merchantId: '', peerUserId: '' }
    await loadSessions()
    selectSession(session)
  } catch {
    // 错误已提示
  }
}

function scrollToBottom() {
  nextTick(() => {
    const last = messages.value[messages.value.length - 1]
    if (last) scrollInto.value = `msg-${last.id}`
  })
}

async function connectHub() {
  const token = uni.getStorageSync('token') as string
  if (!token) return
  try {
    // H5 走 Vite 代理 /hub/chat；App 端可替换为直连网关（骨架阶段 H5 优先）
    const base = import.meta.env.VITE_API_BASE || ''
    connection = new signalR.HubConnectionBuilder()
      .withUrl(`${base}/hub/chat?access_token=${encodeURIComponent(token)}`)
      .withAutomaticReconnect()
      .build()

    connection.on('ReceiveMessage', (msg: ChatMessage) => {
      if (msg.sessionId === activeId.value) {
        messages.value.push(msg)
        scrollToBottom()
      } else {
        loadSessions()
      }
    })

    await connection.start()
  } catch {
    // Hub 不可用时 REST 兜底
  }
}

onLoad(async (query) => {
  await loadSessions()
  connectHub()
})

onUnload(() => {
  connection?.stop().catch(() => {})
})
</script>

<style scoped>
.page { display: flex; height: 100vh; }
.session-panel { width: 260rpx; background: #fff; border-right: 1px solid #eee; display: flex; flex-direction: column; }
.panel-head { padding: 20rpx 16rpx; display: flex; justify-content: space-between; align-items: center; }
.panel-title { font-size: 28rpx; font-weight: 500; }
.new-btn { font-size: 22rpx; color: #e64340; }
.session-list { flex: 1; }
.session-item { padding: 16rpx; border-bottom: 1px solid #f5f6f7; position: relative; }
.session-item.active { background: #fff3f3; }
.s-name { font-size: 26rpx; font-weight: 500; }
.s-preview { font-size: 20rpx; color: #999; margin-top: 6rpx; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.s-badge { position: absolute; top: 12rpx; right: 12rpx; background: #e64340; color: #fff; border-radius: 50%; min-width: 32rpx; height: 32rpx; text-align: center; line-height: 32rpx; font-size: 20rpx; }
.s-empty { padding: 40rpx 16rpx; color: #999; font-size: 22rpx; text-align: center; }
.chat-panel { flex: 1; display: flex; flex-direction: column; }
.msg-list { flex: 1; padding: 20rpx; }
.msg-row { display: flex; margin-bottom: 16rpx; }
.msg-row.mine { justify-content: flex-end; }
.msg-bubble { max-width: 80%; background: #fff; border-radius: 12rpx; padding: 14rpx 20rpx; }
.msg-row.mine .msg-bubble { background: #d9ecff; }
.msg-meta { font-size: 20rpx; color: #999; margin-bottom: 6rpx; }
.msg-content { font-size: 28rpx; word-break: break-all; }
.m-empty { text-align: center; color: #999; padding: 60rpx 0; }
.input-bar { display: flex; padding: 16rpx; background: #fff; border-top: 1px solid #eee; }
.msg-input { flex: 1; height: 68rpx; background: #f5f6f7; border-radius: 34rpx; padding: 0 24rpx; font-size: 28rpx; }
.send-btn { margin-left: 16rpx; background: #e64340; color: #fff; padding: 0 40rpx; border-radius: 34rpx; line-height: 68rpx; font-size: 28rpx; }
.send-btn.disabled { opacity: 0.5; }
.modal-mask { position: fixed; inset: 0; background: rgba(0,0,0,0.4); z-index: 100; display: flex; align-items: center; justify-content: center; }
.modal { width: 560rpx; background: #fff; border-radius: 16rpx; padding: 32rpx; }
.modal-title { font-size: 32rpx; font-weight: 500; text-align: center; margin-bottom: 24rpx; }
.modal-input { border: 1px solid #eee; border-radius: 8rpx; padding: 16rpx 20rpx; margin-bottom: 16rpx; font-size: 26rpx; }
.modal-btns { display: flex; justify-content: flex-end; margin-top: 16rpx; }
.m-btn { padding: 14rpx 40rpx; border-radius: 40rpx; font-size: 28rpx; margin-left: 16rpx; }
.m-btn.cancel { background: #f5f6f7; color: #666; }
.m-btn.ok { background: #e64340; color: #fff; }
</style>

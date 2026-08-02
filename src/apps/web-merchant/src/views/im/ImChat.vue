<template>
  <el-card shadow="never" class="im-card">
    <div class="im-wrap">
      <!-- 会话列表 -->
      <div class="session-panel">
        <div class="panel-title">客服会话</div>
        <div v-loading="loadingSessions" class="session-list">
          <div v-for="s in sessions" :key="s.id" class="session-item" :class="{ active: s.id === activeSessionId }"
               @click="selectSession(s)">
            <div class="session-name">{{ sessionTitle(s) }}</div>
            <div class="session-preview">{{ s.lastMessagePreview || '暂无消息' }}</div>
            <el-badge v-if="s.unreadCount > 0" :value="s.unreadCount" class="unread" />
          </div>
          <el-empty v-if="!loadingSessions && sessions.length === 0" description="暂无会话" :image-size="60" />
        </div>
      </div>

      <!-- 聊天窗口 -->
      <div class="chat-panel">
        <div class="chat-header">
          <b>{{ activeSession ? sessionTitle(activeSession) : '请选择会话' }}</b>
          <span v-if="typingUser" class="typing">对方正在输入…</span>
        </div>
        <div ref="msgListRef" v-loading="loadingMessages" class="msg-list">
          <div v-for="m in messages" :key="m.id" class="msg-row" :class="{ mine: m.senderId === myUserId }">
            <div class="msg-bubble">
              <div class="msg-meta">
                <span>{{ m.senderName }}</span>
                <span class="msg-time">{{ fmtTime(m.createdAt) }}</span>
              </div>
              <div class="msg-content">{{ m.content }}</div>
            </div>
          </div>
          <el-empty v-if="!loadingMessages && messages.length === 0" description="暂无消息，打个招呼吧" :image-size="60" />
        </div>
        <div class="chat-input">
          <el-input v-model="draft" type="textarea" :rows="3" placeholder="输入消息，Enter 发送（Shift+Enter 换行）"
                    @keydown.enter.exact.prevent="send" @input="onInput" />
          <div class="input-actions">
            <el-button type="primary" :disabled="!activeSession || !draft.trim()" @click="send">发送</el-button>
          </div>
        </div>
      </div>
    </div>
  </el-card>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import * as signalR from '@microsoft/signalr'
import { imApi, type ChatMessage, type ChatSession } from '../../api'
import { useAuthStore } from '../../stores/auth'

const auth = useAuthStore()
const myUserId = computed(() => {
  try {
    const payload = JSON.parse(atob(auth.token.split('.')[1]))
    return payload.sub as string
  } catch {
    return ''
  }
})

const sessions = ref<ChatSession[]>([])
const activeSessionId = ref('')
const activeSession = computed(() => sessions.value.find((s) => s.id === activeSessionId.value) || null)
const messages = ref<ChatMessage[]>([])
const draft = ref('')
const loadingSessions = ref(false)
const loadingMessages = ref(false)
const msgListRef = ref<HTMLElement>()
const typingUser = ref('')

let connection: signalR.HubConnection | null = null
let typingTimer: ReturnType<typeof setTimeout> | null = null

function sessionTitle(s: ChatSession) {
  if (s.type === 2 && s.name) return s.name
  // 私聊：显示对方（非自己的成员）
  const peer = s.members.find((m) => m.userId !== myUserId.value)
  return peer ? peer.userName || '客服' : '会话'
}

function fmtTime(t: string) {
  const d = new Date(t)
  const now = new Date()
  const sameDay = d.toDateString() === now.toDateString()
  return sameDay ? d.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' }) : d.toLocaleString('zh-CN')
}

function scrollToBottom() {
  nextTick(() => {
    if (msgListRef.value) {
      msgListRef.value.scrollTop = msgListRef.value.scrollHeight
    }
  })
}

// ---------- 会话列表 ----------
async function loadSessions() {
  loadingSessions.value = true
  try {
    sessions.value = await imApi.merchantSessions()
    if (!activeSessionId.value && sessions.value.length > 0) {
      selectSession(sessions.value[0])
    }
  } finally {
    loadingSessions.value = false
  }
}

// ---------- 消息 ----------
async function loadMessages(sessionId: string) {
  loadingMessages.value = true
  try {
    const res = await imApi.messages(sessionId, { limit: 50 })
    messages.value = res.items
    scrollToBottom()
  } finally {
    loadingMessages.value = false
  }
}

async function selectSession(s: ChatSession) {
  activeSessionId.value = s.id
  typingUser.value = ''
  await loadMessages(s.id)
  // 标记已读（REST + Hub 广播）
  if (s.unreadCount > 0) {
    try {
      await imApi.markRead(s.id)
      connection?.invoke('MarkAsRead', s.id).catch(() => {})
      s.unreadCount = 0
    } catch {
      // 忽略
    }
  }
}

// ---------- 发送 ----------
async function send() {
  const content = draft.value.trim()
  if (!activeSessionId.value || !content) return
  const sessionId = activeSessionId.value
  draft.value = ''
  try {
    // 优先走 SignalR（实时），失败回退 REST
    if (connection && connection.state === 'Connected') {
      const msg = await connection.invoke('SendMessage', sessionId, content, 1)
      messages.value.push(msg)
    } else {
      const msg = await imApi.reply(sessionId, content)
      messages.value.push(msg)
    }
    scrollToBottom()
  } catch {
    // Hub 异常回退 REST
    try {
      const msg = await imApi.reply(sessionId, content)
      messages.value.push(msg)
      scrollToBottom()
    } catch {
      ElMessage.error('发送失败，请重试')
    }
  }
}

function onInput() {
  if (typingTimer) clearTimeout(typingTimer)
  typingTimer = setTimeout(() => {
    if (activeSessionId.value) {
      connection?.invoke('SendTypingIndicator', activeSessionId.value).catch(() => {})
    }
  }, 500)
}

// ---------- SignalR ----------
async function connectHub() {
  const token = localStorage.getItem('token')
  if (!token) return
  connection = new signalR.HubConnectionBuilder()
    .withUrl(`/hub/chat?access_token=${encodeURIComponent(token)}`)
    .withAutomaticReconnect()
    .build()

  connection.on('ReceiveMessage', (msg: ChatMessage) => {
    if (msg.sessionId === activeSessionId.value) {
      messages.value.push(msg)
      scrollToBottom()
    } else {
      // 其他会话来消息 → 刷新列表（未读数）
      loadSessions()
    }
  })
  connection.on('MessageRead', (sessionId: string) => {
    if (sessionId === activeSessionId.value) {
      // 对方已读（无需额外处理，可在此优化 UI）
    }
  })
  connection.on('TypingIndicator', (_sessionId: string, _uid: string, name: string) => {
    typingUser.value = name
    setTimeout(() => { typingUser.value = '' }, 3000)
  })

  try {
    await connection.start()
  } catch {
    // 网关/Hub 未启动时静默（REST 兜底仍可用）
  }
}

onMounted(() => {
  loadSessions()
  connectHub()
})

onBeforeUnmount(() => {
  connection?.stop().catch(() => {})
  if (typingTimer) clearTimeout(typingTimer)
})
</script>

<style scoped>
.im-card { height: calc(100vh - 140px); }
.im-wrap { display: flex; height: 100%; }
.session-panel { width: 260px; border-right: 1px solid #ebeef5; display: flex; flex-direction: column; }
.panel-title { padding: 12px 16px; font-weight: 500; border-bottom: 1px solid #ebeef5; }
.session-list { flex: 1; overflow-y: auto; }
.session-item { position: relative; padding: 10px 16px; cursor: pointer; border-bottom: 1px solid #f5f7fa; }
.session-item:hover { background: #f5f7fa; }
.session-item.active { background: #ecf5ff; }
.session-name { font-size: 14px; font-weight: 500; margin-bottom: 4px; padding-right: 30px; }
.session-preview { font-size: 12px; color: #909399; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.unread { position: absolute; top: 12px; right: 12px; }
.chat-panel { flex: 1; display: flex; flex-direction: column; }
.chat-header { padding: 12px 16px; border-bottom: 1px solid #ebeef5; display: flex; justify-content: space-between; }
.typing { font-size: 12px; color: #909399; }
.msg-list { flex: 1; overflow-y: auto; padding: 16px; background: #fafafa; }
.msg-row { display: flex; margin-bottom: 12px; }
.msg-row.mine { justify-content: flex-end; }
.msg-bubble { max-width: 70%; background: #fff; border-radius: 8px; padding: 8px 12px; box-shadow: 0 1px 2px rgba(0,0,0,0.05); }
.msg-row.mine .msg-bubble { background: #d9ecff; }
.msg-meta { font-size: 11px; color: #909399; margin-bottom: 4px; }
.msg-time { margin-left: 8px; }
.msg-content { font-size: 14px; line-height: 1.5; word-break: break-word; white-space: pre-wrap; }
.chat-input { border-top: 1px solid #ebeef5; padding: 12px; }
.input-actions { display: flex; justify-content: flex-end; margin-top: 8px; }
</style>

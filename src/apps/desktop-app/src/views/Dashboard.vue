<template>
  <div class="dashboard">
    <!-- 公告：最新 3 条 -->
    <el-card shadow="never" class="panel">
      <template #header>
        <div class="panel-header">
          <span>📢 最新公告</span>
          <el-link type="primary" :underline="false" @click="router.push('/announcements')">查看全部</el-link>
        </div>
      </template>
      <el-skeleton v-if="loading.announcements" :rows="3" animated />
      <div v-else-if="announcements.length === 0" class="empty">暂无公告</div>
      <div
        v-for="a in announcements"
        :key="a.id"
        class="row clickable"
        @click="router.push(`/announcements/${a.id}`)"
      >
        <el-badge v-if="!a.isRead" is-dot class="unread-dot" />
        <span class="row-title">{{ a.title }}</span>
        <el-tag size="small" :type="categoryTag(a.category)">{{ categoryText(a.category) }}</el-tag>
        <span class="row-time">{{ formatTime(a.publishedAt) }}</span>
      </div>
    </el-card>

    <!-- 通知：最新 5 条 -->
    <el-card shadow="never" class="panel">
      <template #header>
        <div class="panel-header">
          <span>🔔 最近通知</span>
          <el-link type="primary" :underline="false" @click="router.push('/notifications')">查看全部</el-link>
        </div>
      </template>
      <el-skeleton v-if="loading.notifications" :rows="5" animated />
      <div v-else-if="notifications.length === 0" class="empty">暂无通知</div>
      <div
        v-for="n in notifications"
        :key="n.id"
        class="row clickable"
        :class="{ unread: !n.isRead }"
        @click="openNotification(n)"
      >
        <span class="row-title">{{ n.title }}</span>
        <span class="row-time">{{ formatTime(n.createdAt) }}</span>
      </div>
    </el-card>

    <!-- 内部邮件：最新 5 封 -->
    <el-card shadow="never" class="panel">
      <template #header>
        <div class="panel-header">
          <span>✉️ 内部邮件</span>
          <el-link type="primary" :underline="false" @click="router.push('/emails')">查看全部</el-link>
        </div>
      </template>
      <el-skeleton v-if="loading.emails" :rows="5" animated />
      <div v-else-if="emails.length === 0" class="empty">暂无邮件</div>
      <div v-for="m in emails" :key="m.id" class="row clickable" @click="openEmail(m)">
        <span class="row-title">{{ m.subject }}</span>
        <span class="row-sub">{{ m.from }} → {{ m.to }}</span>
        <el-tag size="small" :type="statusTag(m.status)">{{ statusText(m.status) }}</el-tag>
        <span class="row-time">{{ formatTime(m.createdAt) }}</span>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { announcementsApi, type Announcement, type AnnouncementCategory } from '../api/announcements'
import { emailsApi, type EmailItem, type EmailStatus } from '../api/emails'
import { notificationsApi, type NotificationItem } from '../api/notifications'

const router = useRouter()
const announcements = ref<Announcement[]>([])
const notifications = ref<NotificationItem[]>([])
const emails = ref<EmailItem[]>([])
const loading = reactive({ announcements: true, notifications: true, emails: true })

onMounted(async () => {
  try {
    const a = await announcementsApi.list(undefined, 1, 3)
    announcements.value = a.items
  } finally {
    loading.announcements = false
  }
  try {
    const n = await notificationsApi.list(undefined, undefined, 1, 5)
    notifications.value = n.items
  } finally {
    loading.notifications = false
  }
  try {
    const e = await emailsApi.list(undefined, undefined, 1, 5)
    emails.value = e.items
  } finally {
    loading.emails = false
  }
})

function openNotification(n: NotificationItem) {
  if (!n.isRead) {
    notificationsApi.markRead(n.id).catch(() => undefined)
  }
  router.push('/notifications')
}

function openEmail(m: EmailItem) {
  router.push('/emails')
}

function categoryText(c: AnnouncementCategory) {
  return c === 1 ? '系统公告' : c === 2 ? '运营公告' : '维护公告'
}
function categoryTag(c: AnnouncementCategory) {
  return c === 1 ? 'danger' : c === 2 ? 'primary' : 'warning'
}
function statusText(s: EmailStatus) {
  return s === 0 ? '待发送' : s === 1 ? '已发送' : s === 2 ? '失败' : '死信'
}
function statusTag(s: EmailStatus) {
  return s === 1 ? 'success' : s === 2 ? 'danger' : s === 3 ? 'info' : 'warning'
}
function formatTime(t: string | null) {
  if (!t) return ''
  return t.replace('T', ' ').slice(0, 16)
}
</script>

<style scoped>
.dashboard {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 4px;
  border-bottom: 1px solid #f0f2f5;
  font-size: 14px;
}
.row.clickable {
  cursor: pointer;
}
.row.clickable:hover {
  background: #f5f7fa;
}
.row.unread {
  font-weight: 600;
}
.row-title {
  color: #303133;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 320px;
}
.row-sub {
  color: #909399;
  font-size: 12px;
  max-width: 200px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.row-time {
  margin-left: auto;
  color: #909399;
  font-size: 12px;
  white-space: nowrap;
}
.unread-dot {
  flex-shrink: 0;
}
.empty {
  color: #909399;
  font-size: 13px;
  padding: 12px 0;
  text-align: center;
}
</style>

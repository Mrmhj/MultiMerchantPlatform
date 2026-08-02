<template>
  <el-card shadow="never">
    <template #header>
      <div class="list-header">
        <span>通知收件箱（未读 {{ unread }}）</span>
        <el-button size="small" type="primary" plain :disabled="unread === 0" @click="markAllRead">
          全部已读
        </el-button>
      </div>
    </template>

    <el-skeleton v-if="loading" :rows="8" animated />
    <el-empty v-else-if="list.length === 0" description="暂无通知" />
    <template v-else>
      <div v-for="n in list" :key="n.id" class="item" :class="{ unread: !n.isRead }">
        <div class="item-main">
          <div class="item-title">
            <el-tag size="small" :type="typeTag(n.type)">{{ typeText(n.type) }}</el-tag>
            <span class="title">{{ n.title }}</span>
          </div>
          <div class="item-content">{{ n.content }}</div>
          <div class="item-meta">
            {{ formatTime(n.createdAt) }}
            <span v-if="n.bizId"> · {{ n.bizType }}:{{ n.bizId }}</span>
          </div>
        </div>
        <div class="item-actions">
          <el-button v-if="!n.isRead" size="small" @click="markRead(n)">标记已读</el-button>
          <el-button size="small" type="danger" plain @click="remove(n)">删除</el-button>
        </div>
      </div>

      <el-pagination
        class="pager"
        layout="prev, pager, next"
        :total="total"
        :page-size="pageSize"
        :current-page="page"
        @current-change="load"
      />
    </template>
  </el-card>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { notificationsApi, type NotificationItem, type NotificationType } from '../../api/notifications'

const list = ref<NotificationItem[]>([])
const total = ref(0)
const unread = ref(0)
const page = ref(1)
const pageSize = 20
const loading = ref(true)

onMounted(() => load(1))

async function load(p: number) {
  page.value = p
  loading.value = true
  try {
    const [res, count] = await Promise.all([
      notificationsApi.list(undefined, undefined, p, pageSize),
      notificationsApi.unreadCount(),
    ])
    list.value = res.items
    total.value = res.totalCount
    unread.value = count.unreadCount
  } finally {
    loading.value = false
  }
}

async function markRead(n: NotificationItem) {
  await notificationsApi.markRead(n.id)
  n.isRead = true
  unread.value = Math.max(0, unread.value - 1)
}

async function markAllRead() {
  await notificationsApi.markAllRead()
  list.value.forEach((n) => (n.isRead = true))
  unread.value = 0
  ElMessage.success('已全部标记为已读')
}

async function remove(n: NotificationItem) {
  await ElMessageBox.confirm('确定删除这条通知吗？', '提示', { type: 'warning' })
  await notificationsApi.remove(n.id)
  list.value = list.value.filter((x) => x.id !== n.id)
  total.value = Math.max(0, total.value - 1)
  if (!n.isRead) unread.value = Math.max(0, unread.value - 1)
}

function typeText(t: NotificationType) {
  const map: Record<number, string> = {
    1: '订单', 2: '支付', 3: '物流', 4: '营销', 5: '系统', 6: '风控', 7: '监控',
  }
  return map[t] || '系统'
}
function typeTag(t: NotificationType) {
  const map: Record<number, 'danger' | 'primary' | 'warning' | 'info'> = {
    1: 'primary', 2: 'danger', 3: 'warning', 4: 'info', 5: 'info', 6: 'danger', 7: 'warning',
  }
  return map[t] || 'info'
}
function formatTime(t: string) {
  return t.replace('T', ' ').slice(0, 16)
}
</script>

<style scoped>
.list-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.item {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  padding: 14px 4px;
  border-bottom: 1px solid #f0f2f5;
}
.item.unread {
  background: #fafcff;
}
.item-title {
  display: flex;
  align-items: center;
  gap: 8px;
}
.title {
  font-size: 14px;
  font-weight: 600;
  color: #303133;
}
.item-content {
  margin-top: 6px;
  font-size: 13px;
  color: #606266;
  line-height: 1.6;
}
.item-meta {
  margin-top: 6px;
  font-size: 12px;
  color: #909399;
}
.item-actions {
  flex-shrink: 0;
  display: flex;
  gap: 8px;
}
.pager {
  margin-top: 16px;
  justify-content: flex-end;
}
</style>

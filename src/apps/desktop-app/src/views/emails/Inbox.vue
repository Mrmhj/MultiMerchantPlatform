<template>
  <el-card shadow="never">
    <template #header>
      <div class="list-header">
        <span>内部邮件（{{ total }}）</span>
        <div class="actions">
          <el-radio-group v-model="status" size="small" @change="load(1)">
            <el-radio-button :value="undefined">全部</el-radio-button>
            <el-radio-button :value="1">已发送</el-radio-button>
            <el-radio-button :value="2">失败</el-radio-button>
          </el-radio-group>
          <el-button type="primary" size="small" @click="router.push('/emails/compose')">
            写邮件
          </el-button>
        </div>
      </div>
    </template>

    <el-skeleton v-if="loading" :rows="8" animated />
    <el-empty v-else-if="list.length === 0" description="暂无邮件" />
    <template v-else>
      <div v-for="m in list" :key="m.id" class="item" @click="openDetail(m)">
        <span class="subject">{{ m.subject }}</span>
        <span class="meta">{{ m.from }} → {{ m.to }}</span>
        <el-tag size="small" :type="statusTag(m.status)">{{ statusText(m.status) }}</el-tag>
        <span class="time">{{ formatTime(m.createdAt) }}</span>
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

    <!-- 邮件详情抽屉 -->
    <el-drawer v-model="drawerVisible" :title="current?.subject || '邮件详情'" size="520px">
      <template v-if="current">
        <el-descriptions :column="1" border size="small">
          <el-descriptions-item label="发件人">{{ current.from }}</el-descriptions-item>
          <el-descriptions-item label="收件人">{{ current.to }}</el-descriptions-item>
          <el-descriptions-item label="状态">{{ statusText(current.status) }}</el-descriptions-item>
          <el-descriptions-item label="发送时间">
            {{ formatTime(current.sentAt || current.createdAt) }}
          </el-descriptions-item>
          <el-descriptions-item v-if="current.lastError" label="错误信息">
            <span class="error">{{ current.lastError }}</span>
          </el-descriptions-item>
        </el-descriptions>
        <el-divider />
        <div class="mail-body">{{ mailBody }}</div>
        <div v-if="current.status === 2 || current.status === 3" class="retry">
          <el-button size="small" type="primary" @click="retry(current)">手动重试</el-button>
        </div>
      </template>
    </el-drawer>
  </el-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { emailsApi, type EmailItem, type EmailStatus } from '../../api/emails'

const router = useRouter()
const list = ref<EmailItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = 20
const status = ref<EmailStatus | undefined>(undefined)
const loading = ref(true)
const drawerVisible = ref(false)
const current = ref<EmailItem | null>(null)

// 邮件正文由 email-service 落库（DryRun 不真实外发，正文仅内部可见）
const mailBody = computed(() => {
  const item = current.value
  if (!item?.body) return '（无正文内容）'
  return item.body
})

onMounted(() => load(1))

async function load(p: number) {
  page.value = p
  loading.value = true
  try {
    const res = await emailsApi.list(status.value, undefined, p, pageSize)
    list.value = res.items
    total.value = res.totalCount
  } finally {
    loading.value = false
  }
}

async function openDetail(m: EmailItem) {
  current.value = await emailsApi.detail(m.id)
  drawerVisible.value = true
}

async function retry(m: EmailItem) {
  current.value = await emailsApi.retry(m.id)
  ElMessage.success('已重新加入发送队列')
  load(page.value)
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
.list-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.actions {
  display: flex;
  align-items: center;
  gap: 12px;
}
.item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 4px;
  border-bottom: 1px solid #f0f2f5;
  font-size: 14px;
  cursor: pointer;
}
.item:hover {
  background: #f5f7fa;
}
.subject {
  color: #303133;
  font-weight: 500;
  max-width: 280px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.meta {
  color: #909399;
  font-size: 12px;
  max-width: 240px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.time {
  margin-left: auto;
  color: #909399;
  font-size: 12px;
  white-space: nowrap;
}
.pager {
  margin-top: 16px;
  justify-content: flex-end;
}
.mail-body {
  min-height: 80px;
  color: #606266;
  font-size: 13px;
  line-height: 1.8;
}
.error {
  color: #f56c6c;
  word-break: break-all;
}
.retry {
  margin-top: 16px;
}
</style>

<template>
  <el-card shadow="never" v-if="detail">
    <template #header>
      <div class="detail-header">
        <el-button size="small" @click="router.back()">← 返回</el-button>
        <el-tag size="small" :type="tagType(detail.category)">{{ categoryText(detail.category) }}</el-tag>
        <el-tag size="small" :type="detail.isRead ? 'info' : 'danger'" effect="plain">
          {{ detail.isRead ? '已读' : '未读' }}
        </el-tag>
      </div>
    </template>

    <h2 class="title">{{ detail.title }}</h2>
    <div class="meta">
      发布者：{{ detail.publisherName }} · 发布时间：{{ formatTime(detail.publishedAt) }}
    </div>
    <el-divider />
    <div class="content">{{ detail.content }}</div>
    <div class="actions">
      <el-button v-if="!detail.isRead" type="primary" @click="markRead">标记已读</el-button>
    </div>
  </el-card>
  <el-skeleton v-else :rows="6" animated />
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { announcementsApi, type Announcement, type AnnouncementCategory } from '../../api/announcements'

const route = useRoute()
const router = useRouter()
const detail = ref<Announcement | null>(null)

onMounted(async () => {
  try {
    detail.value = await announcementsApi.detail(route.params.id as string)
  } catch {
    // 错误由拦截器提示
  }
})

async function markRead() {
  detail.value = await announcementsApi.markRead(detail.value!.id)
  ElMessage.success('已标记为已读')
}

function categoryText(c: AnnouncementCategory) {
  return c === 1 ? '系统公告' : c === 2 ? '运营公告' : '维护公告'
}
function tagType(c: AnnouncementCategory) {
  return c === 1 ? 'danger' : c === 2 ? 'primary' : 'warning'
}
function formatTime(t: string | null) {
  if (!t) return ''
  return t.replace('T', ' ').slice(0, 16)
}
</script>

<style scoped>
.detail-header {
  display: flex;
  align-items: center;
  gap: 10px;
}
.title {
  margin: 8px 0 4px;
  font-size: 20px;
}
.meta {
  color: #909399;
  font-size: 13px;
}
.content {
  font-size: 14px;
  line-height: 1.9;
  white-space: pre-wrap;
  color: #303133;
}
.actions {
  margin-top: 24px;
}
</style>

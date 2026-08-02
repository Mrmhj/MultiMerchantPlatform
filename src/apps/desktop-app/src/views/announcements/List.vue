<template>
  <el-card shadow="never">
    <template #header>
      <div class="list-header">
        <span>平台公告（{{ total }}）</span>
        <el-radio-group v-model="category" size="small" @change="load(1)">
          <el-radio-button :value="undefined">全部</el-radio-button>
          <el-radio-button :value="1">系统公告</el-radio-button>
          <el-radio-button :value="2">运营公告</el-radio-button>
          <el-radio-button :value="3">维护公告</el-radio-button>
        </el-radio-group>
      </div>
    </template>

    <el-skeleton v-if="loading" :rows="8" animated />
    <el-empty v-else-if="list.length === 0" description="暂无公告" />
    <template v-else>
      <div
        v-for="a in list"
        :key="a.id"
        class="item clickable"
        @click="router.push(`/announcements/${a.id}`)"
      >
        <el-badge v-if="!a.isRead" is-dot class="dot" />
        <span class="title">{{ a.title }}</span>
        <el-tag size="small" :type="tagType(a.category)">{{ categoryText(a.category) }}</el-tag>
        <span class="meta">{{ a.publisherName }} · {{ formatTime(a.publishedAt) }}</span>
        <el-tag v-if="a.isRead" size="small" type="info" effect="plain">已读</el-tag>
        <el-tag v-else size="small" type="danger" effect="plain">未读</el-tag>
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
import { useRouter } from 'vue-router'
import { announcementsApi, type Announcement, type AnnouncementCategory } from '../../api/announcements'

const router = useRouter()
const list = ref<Announcement[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = 20
const category = ref<AnnouncementCategory | undefined>(undefined)
const loading = ref(true)

onMounted(() => load(1))

async function load(p: number) {
  page.value = p
  loading.value = true
  try {
    const res = await announcementsApi.list(category.value, p, pageSize)
    list.value = res.items
    total.value = res.totalCount
  } finally {
    loading.value = false
  }
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
.list-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 4px;
  border-bottom: 1px solid #f0f2f5;
  font-size: 14px;
}
.item.clickable {
  cursor: pointer;
}
.item.clickable:hover {
  background: #f5f7fa;
}
.title {
  color: #303133;
  font-weight: 500;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 420px;
}
.meta {
  margin-left: auto;
  color: #909399;
  font-size: 12px;
  white-space: nowrap;
}
.dot {
  flex-shrink: 0;
}
.pager {
  margin-top: 16px;
  justify-content: flex-end;
}
</style>

<template>
  <el-container class="main-layout">
    <!-- 侧边栏 -->
    <el-aside width="220px" class="aside">
      <div class="logo">
        <span class="logo-icon">🛍️</span>
        <span class="logo-text">摩登商户工作台</span>
      </div>
      <el-menu :default-active="activeMenu" router class="menu">
        <el-menu-item index="/dashboard">
          <el-icon><HomeFilled /></el-icon>
          <span>工作台首页</span>
        </el-menu-item>
        <el-menu-item index="/announcements">
          <el-icon><BellFilled /></el-icon>
          <span>平台公告</span>
          <el-badge v-if="announcementUnread > 0" :value="announcementUnread" class="menu-badge" />
        </el-menu-item>
        <el-menu-item index="/emails">
          <el-icon><Message /></el-icon>
          <span>内部邮件</span>
        </el-menu-item>
        <el-menu-item index="/notifications">
          <el-icon><Notification /></el-icon>
          <span>通知收件箱</span>
          <el-badge v-if="notificationUnread > 0" :value="notificationUnread" class="menu-badge" />
        </el-menu-item>
      </el-menu>
    </el-aside>

    <!-- 主区 -->
    <el-container>
      <el-header class="header">
        <div class="header-title">{{ pageTitle }}</div>
        <div class="header-right">
          <el-tag v-if="auth.isAdmin" size="small" type="danger" effect="plain">管理员</el-tag>
          <el-tag v-if="auth.isApproved" size="small" type="success" effect="plain">
            {{ auth.merchantName }}
          </el-tag>
          <span class="user-name">{{ auth.displayName }}</span>
          <el-button size="small" @click="onLogout">退出登录</el-button>
        </div>
      </el-header>
      <el-main class="main">
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { BellFilled, HomeFilled, Message, Notification } from '@element-plus/icons-vue'
import { ElMessageBox } from 'element-plus'
import { useAuthStore } from '../../stores/auth'
import { announcementsApi } from '../../api/announcements'
import { notificationsApi } from '../../api/notifications'
import {
  connectNotificationHub,
  disconnectNotificationHub,
  onReceiveAnnouncement,
  onReceiveNotification,
  onUnreadCountChanged,
} from '../../signalr/notification'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const announcementUnread = ref(0)
const notificationUnread = ref(0)

const activeMenu = computed(() => route.path)
const pageTitle = computed(() => {
  const map: Record<string, string> = {
    '/dashboard': '工作台首页',
    '/announcements': '平台公告',
    '/emails': '内部邮件',
    '/notifications': '通知收件箱',
  }
  if (route.path.startsWith('/announcements/')) return '公告详情'
  if (route.path.startsWith('/emails/compose')) return '写邮件'
  return map[route.path] || '工作台'
})

let unsubs: Array<() => void> = []

onMounted(async () => {
  await refreshBadges()
  setupRealtime()
})

onUnmounted(() => {
  unsubs.forEach((fn) => fn())
  unsubs = []
})

async function refreshBadges() {
  try {
    const [a, n] = await Promise.all([
      announcementsApi.unreadCount(),
      notificationsApi.unreadCount(),
    ])
    announcementUnread.value = a.unreadCount
    notificationUnread.value = n.unreadCount
  } catch {
    // 服务未就绪时忽略
  }
}

function setupRealtime() {
  connectNotificationHub(auth.token)
  unsubs.push(
    onReceiveNotification(() => {
      notificationUnread.value += 1
    }),
    onUnreadCountChanged((count) => {
      notificationUnread.value = count
    }),
    onReceiveAnnouncement(() => {
      announcementUnread.value += 1
    }),
  )
}

async function onLogout() {
  await ElMessageBox.confirm('确定退出登录吗？', '提示', { type: 'warning' })
  disconnectNotificationHub()
  auth.logout()
  router.push({ name: 'login' })
}
</script>

<style scoped>
.main-layout {
  height: 100vh;
}
.aside {
  background: #fff;
  border-right: 1px solid #e4e7ed;
}
.logo {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 16px 20px;
  font-weight: 600;
  font-size: 15px;
  border-bottom: 1px solid #f0f2f5;
}
.logo-icon {
  font-size: 20px;
}
.menu {
  border-right: none;
}
.menu-badge {
  margin-left: auto;
}
.header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: #fff;
  border-bottom: 1px solid #e4e7ed;
}
.header-title {
  font-size: 16px;
  font-weight: 500;
}
.header-right {
  display: flex;
  align-items: center;
  gap: 10px;
}
.user-name {
  color: #606266;
}
.main {
  background: #f5f7fa;
}
</style>

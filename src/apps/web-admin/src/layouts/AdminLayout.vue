<template>
  <div class="layout">
    <el-container style="height: 100vh">
      <el-aside width="220px" class="aside">
        <div class="logo">多商户商城</div>
        <div class="logo-sub">平台管理后台</div>
        <el-menu :default-active="activeMenu" router background-color="#001529" text-color="#b7c0cd"
                 active-text-color="#ffffff" style="border-right: none">
          <el-menu-item index="/dashboard"><el-icon><DataAnalysis /></el-icon>BI 数据看板</el-menu-item>
        </el-menu>
      </el-aside>
      <el-container>
        <el-header class="header">
          <div class="header-title">{{ pageTitle }}</div>
          <div class="header-right">
            <el-tag type="success" size="small">{{ auth.user?.displayName || '管理员' }}</el-tag>
            <el-dropdown @command="onCommand">
              <span class="user-name">{{ auth.user?.email || 'admin' }}</span>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item command="logout">退出登录</el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
          </div>
        </el-header>
        <el-main class="main">
          <router-view />
        </el-main>
      </el-container>
    </el-container>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { DataAnalysis } from '@element-plus/icons-vue'
import { useAuthStore } from '../stores/auth'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const activeMenu = computed(() => route.path)
const pageTitle = computed(() => {
  const map: Record<string, string> = { '/dashboard': 'BI 数据看板' }
  return map[route.path] || '平台管理后台'
})

function onCommand(cmd: string) {
  if (cmd === 'logout') {
    auth.logout()
    router.push({ name: 'login' })
  }
}
</script>

<style scoped>
.aside { background: #001529; }
.logo { color: #fff; font-size: 16px; font-weight: 500; text-align: center; padding: 18px 0 4px; }
.logo-sub { color: #7d8ba3; font-size: 12px; text-align: center; padding-bottom: 14px; }
.header { display: flex; align-items: center; justify-content: space-between; background: #fff; border-bottom: 1px solid #e6e6e6; }
.header-title { font-size: 16px; font-weight: 500; }
.header-right { display: flex; align-items: center; gap: 12px; }
.user-name { cursor: pointer; color: #333; }
.main { background: #f5f7fa; padding: 16px; }
</style>

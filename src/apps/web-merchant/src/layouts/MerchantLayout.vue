<template>
  <div class="layout">
    <el-container style="height: 100vh">
      <el-aside width="220px" class="aside">
        <div class="logo">多商户商城</div>
        <el-menu :default-active="activeMenu" router background-color="#001529" text-color="#b7c0cd"
                 active-text-color="#ffffff" style="border-right: none">
          <el-menu-item index="/dashboard"><el-icon><Odometer /></el-icon>工作台</el-menu-item>
          <el-menu-item index="/products"><el-icon><Goods /></el-icon>商品管理</el-menu-item>
          <el-menu-item index="/orders"><el-icon><List /></el-icon>订单管理</el-menu-item>
          <el-menu-item index="/stocks"><el-icon><Box /></el-icon>库存管理</el-menu-item>
          <el-menu-item index="/marketing"><el-icon><Present /></el-icon>营销中心</el-menu-item>
          <el-menu-item index="/reviews"><el-icon><ChatDotRound /></el-icon>评价管理</el-menu-item>
          <el-menu-item index="/shipments"><el-icon><Van /></el-icon>物流管理</el-menu-item>
          <el-menu-item index="/settlements"><el-icon><Money /></el-icon>结算管理</el-menu-item>
          <el-menu-item index="/im"><el-icon><Message /></el-icon>在线客服</el-menu-item>
        </el-menu>
      </el-aside>
      <el-container>
        <el-header class="header">
          <div class="header-title">{{ pageTitle }}</div>
          <div class="header-right">
            <el-tag v-if="auth.isApproved" type="success" size="small">{{ auth.merchant?.name }}</el-tag>
            <el-tag v-else type="warning" size="small">未入驻/审核中</el-tag>
            <el-dropdown @command="onCommand">
              <span class="user-name">{{ auth.merchant?.contactName || '商户' }}</span>
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
import { computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Odometer, Goods, List, Box, Present, ChatDotRound, Van, Money, Message } from '@element-plus/icons-vue'
import { useAuthStore } from '../stores/auth'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const activeMenu = computed(() => route.path)
const pageTitle = computed(() => {
  const map: Record<string, string> = {
    '/dashboard': '工作台', '/products': '商品管理', '/orders': '订单管理', '/stocks': '库存管理',
    '/marketing': '营销中心', '/reviews': '评价管理', '/shipments': '物流管理',
    '/settlements': '结算管理', '/im': '在线客服', '/apply': '商户入驻',
  }
  return map[route.path] || '商户中心'
})

onMounted(() => {
  if (!auth.merchant) {
    auth.fetchMerchant()
  }
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
.logo { color: #fff; font-size: 16px; font-weight: 500; text-align: center; padding: 18px 0; }
.header { display: flex; align-items: center; justify-content: space-between; background: #fff; border-bottom: 1px solid #e6e6e6; }
.header-title { font-size: 16px; font-weight: 500; }
.header-right { display: flex; align-items: center; gap: 12px; }
.user-name { cursor: pointer; color: #333; }
.main { background: #f5f7fa; padding: 16px; }
</style>

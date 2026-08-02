<template>
  <el-container class="app-layout">
    <el-header class="app-header">
      <div class="header-inner">
        <router-link to="/" class="brand">摩登商城</router-link>
        <el-menu mode="horizontal" :ellipsis="false" router class="nav-menu">
          <el-menu-item index="/">首页</el-menu-item>
          <el-menu-item index="/orders">我的订单</el-menu-item>
        </el-menu>
        <div class="header-user">
          <template v-if="auth.isAuthenticated">
            <el-dropdown>
              <span class="user-name">
                <el-icon><User /></el-icon>
                {{ auth.user?.displayName || auth.user?.email }}
              </span>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item @click="logout">退出登录</el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
          </template>
          <template v-else>
            <el-button size="small" @click="$router.push('/login')">登录</el-button>
            <el-button size="small" type="primary" @click="$router.push('/register')">注册</el-button>
          </template>
        </div>
      </div>
    </el-header>
    <el-main class="app-main">
      <router-view />
    </el-main>
  </el-container>
</template>

<script setup lang="ts">
import { User } from '@element-plus/icons-vue'
import { useAuthStore } from './stores/auth'
import { useRouter } from 'vue-router'

const auth = useAuthStore()
const router = useRouter()

function logout() {
  auth.logout()
  router.push('/')
}
</script>

<style>
* { margin: 0; padding: 0; box-sizing: border-box; }
body { font-family: 'Helvetica Neue', Helvetica, 'PingFang SC', 'Microsoft YaHei', Arial, sans-serif; background: #f5f7fa; }
.app-layout { min-height: 100vh; }
.app-header { background: #fff; border-bottom: 1px solid #e4e7ed; padding: 0; }
.header-inner { max-width: 1200px; margin: 0 auto; display: flex; align-items: center; gap: 24px; height: 60px; padding: 0 16px; }
.brand { font-size: 20px; font-weight: 700; color: #e4393c; text-decoration: none; white-space: nowrap; }
.nav-menu { border-bottom: none; flex: 1; }
.header-user { display: flex; align-items: center; gap: 8px; }
.user-name { display: flex; align-items: center; gap: 4px; cursor: pointer; color: #606266; }
.app-main { max-width: 1200px; margin: 0 auto; width: 100%; padding: 20px 16px; }
</style>

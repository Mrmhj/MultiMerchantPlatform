<template>
  <div class="login-page">
    <el-card class="auth-card">
      <template #header>
        <div class="card-title">多商户商城 · 平台管理后台</div>
      </template>
      <el-form :model="form" label-width="70px" @keyup.enter="onLogin">
        <el-form-item label="邮箱">
          <el-input v-model="form.email" placeholder="管理员邮箱" />
        </el-form-item>
        <el-form-item label="密码">
          <el-input v-model="form.password" type="password" show-password placeholder="登录密码" />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" style="width: 100%" :loading="loading" @click="onLogin">
            登录
          </el-button>
        </el-form-item>
      </el-form>
      <div class="tip">仅平台管理员账号可登录（role: admin）</div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { authApi } from '../api'
import { useAuthStore } from '../stores/auth'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()
const loading = ref(false)
const form = reactive({ email: '', password: '' })

async function onLogin() {
  if (!form.email || !form.password) {
    ElMessage.warning('请输入邮箱和密码')
    return
  }
  loading.value = true
  try {
    const res = await authApi.login({ email: form.email, password: form.password })
    auth.setSession(res.token, res.user)
    const redirect = (route.query.redirect as string) || '/dashboard'
    router.push(redirect)
  } catch {
    // 错误已由 http 拦截器统一提示
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.login-page { min-height: 100vh; display: flex; align-items: center; justify-content: center; background: #f5f7fa; }
.auth-card { width: 420px; }
.card-title { font-size: 16px; font-weight: 500; text-align: center; }
.tip { font-size: 12px; color: #909399; text-align: center; }
</style>

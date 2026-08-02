<template>
  <el-card class="auth-card">
    <h2 class="title">登录</h2>
    <el-form ref="formRef" :model="form" :rules="rules" label-width="0" size="large">
      <el-form-item prop="email">
        <el-input v-model="form.email" placeholder="邮箱" :prefix-icon="Message" />
      </el-form-item>
      <el-form-item prop="password">
        <el-input v-model="form.password" type="password" placeholder="密码" show-password :prefix-icon="Lock" @keyup.enter="submit" />
      </el-form-item>
      <el-form-item>
        <el-button type="primary" class="submit-btn" :loading="loading" @click="submit">登 录</el-button>
      </el-form-item>
    </el-form>
    <div class="footer">
      还没有账号？<router-link to="/register">立即注册</router-link>
    </div>
  </el-card>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { Lock, Message } from '@element-plus/icons-vue'
import { authApi } from '../api'
import { useAuthStore } from '../stores/auth'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()
const formRef = ref<FormInstance>()
const loading = ref(false)

const form = reactive({ email: '', password: '' })
const rules: FormRules = {
  email: [{ required: true, type: 'email', message: '请输入邮箱', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
}

async function submit() {
  await formRef.value?.validate()
  loading.value = true
  try {
    const res = await authApi.login(form)
    auth.setAuth(res.token, res.user)
    ElMessage.success('登录成功')
    router.push((route.query.redirect as string) || '/')
  } catch {
    // 拦截器已提示
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.auth-card { max-width: 400px; margin: 60px auto; padding: 24px; }
.title { text-align: center; margin-bottom: 24px; }
.submit-btn { width: 100%; }
.footer { text-align: center; color: #909399; font-size: 13px; }
.footer a { color: #409eff; text-decoration: none; }
</style>

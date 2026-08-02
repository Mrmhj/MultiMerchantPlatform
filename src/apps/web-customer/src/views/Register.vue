<template>
  <el-card class="auth-card">
    <h2 class="title">注册</h2>
    <el-form ref="formRef" :model="form" :rules="rules" label-width="0" size="large">
      <el-form-item prop="email">
        <el-input v-model="form.email" placeholder="邮箱" :prefix-icon="Message" />
      </el-form-item>
      <el-form-item prop="displayName">
        <el-input v-model="form.displayName" placeholder="显示名称（可选）" :prefix-icon="User" />
      </el-form-item>
      <el-form-item prop="password">
        <el-input v-model="form.password" type="password" placeholder="密码（至少 6 位）" show-password :prefix-icon="Lock" />
      </el-form-item>
      <el-form-item prop="confirm">
        <el-input v-model="form.confirm" type="password" placeholder="确认密码" show-password :prefix-icon="Lock" @keyup.enter="submit" />
      </el-form-item>
      <el-form-item>
        <el-button type="primary" class="submit-btn" :loading="loading" @click="submit">注 册</el-button>
      </el-form-item>
    </el-form>
    <div class="footer">已有账号？<router-link to="/login">去登录</router-link></div>
  </el-card>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { Lock, Message, User } from '@element-plus/icons-vue'
import { authApi } from '../api'
import { useAuthStore } from '../stores/auth'

const router = useRouter()
const auth = useAuthStore()
const formRef = ref<FormInstance>()
const loading = ref(false)

const form = reactive({ email: '', displayName: '', password: '', confirm: '' })
const rules: FormRules = {
  email: [{ required: true, type: 'email', message: '请输入邮箱', trigger: 'blur' }],
  password: [
    { required: true, min: 6, message: '密码至少 6 位', trigger: 'blur' },
    {
      validator: (_r, v, cb) => (v === form.confirm ? cb() : cb(new Error('两次密码不一致'))),
      trigger: 'blur',
    },
  ],
  confirm: [
    { required: true, message: '请确认密码', trigger: 'blur' },
    {
      validator: (_r, v, cb) => (v === form.password ? cb() : cb(new Error('两次密码不一致'))),
      trigger: 'blur',
    },
  ],
}

async function submit() {
  await formRef.value?.validate()
  loading.value = true
  try {
    const res = await authApi.register({ email: form.email, password: form.password, displayName: form.displayName || undefined })
    auth.setAuth(res.token, res.user)
    ElMessage.success('注册成功，已自动登录')
    router.push('/')
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

<template>
  <view class="page">
    <view class="tabs">
      <view class="tab" :class="{ active: mode === 'login' }" @click="mode = 'login'">登录</view>
      <view class="tab" :class="{ active: mode === 'register' }" @click="mode = 'register'">注册</view>
    </view>

    <view class="form-card">
      <view class="field">
        <text class="label">邮箱</text>
        <input v-model="email" class="input" placeholder="登录邮箱" />
      </view>
      <view class="field">
        <text class="label">密码</text>
        <input v-model="password" class="input" password placeholder="密码（至少 6 位）" />
      </view>
      <view v-if="mode === 'register'" class="field">
        <text class="label">昵称</text>
        <input v-model="displayName" class="input" placeholder="显示昵称（选填）" />
      </view>

      <view class="submit-btn" :class="{ disabled: submitting }" @click="submit">
        {{ mode === 'login' ? '登录' : '注册并登录' }}
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useAuthStore } from '../../stores/auth'

const auth = useAuthStore()
const mode = ref<'login' | 'register'>('login')
const email = ref('')
const password = ref('')
const displayName = ref('')
const submitting = ref(false)

async function submit() {
  if (!email.value || !password.value) {
    uni.showToast({ title: '请输入邮箱和密码', icon: 'none' })
    return
  }
  submitting.value = true
  try {
    if (mode.value === 'login') {
      await auth.login(email.value.trim(), password.value)
    } else {
      await auth.register(email.value.trim(), password.value, displayName.value.trim() || email.value.split('@')[0])
    }
    uni.showToast({ title: '成功', icon: 'success' })
    setTimeout(() => uni.navigateBack({ delta: 1 }), 600)
  } catch {
    // 错误已提示
  } finally {
    submitting.value = false
  }
}
</script>

<style scoped>
.tabs { display: flex; background: #fff; }
.tab { flex: 1; text-align: center; padding: 28rpx 0; font-size: 30rpx; color: #666; }
.tab.active { color: #e64340; font-weight: 500; border-bottom: 4rpx solid #e64340; }
.form-card { background: #fff; margin: 16rpx; border-radius: 12rpx; padding: 32rpx; }
.field { display: flex; align-items: center; padding: 24rpx 0; border-bottom: 1px solid #f5f6f7; }
.label { width: 140rpx; font-size: 28rpx; color: #666; }
.input { flex: 1; font-size: 28rpx; }
.submit-btn { margin-top: 48rpx; background: #e64340; color: #fff; text-align: center; padding: 26rpx 0; border-radius: 44rpx; font-size: 30rpx; }
.submit-btn.disabled { opacity: 0.6; }
</style>

<template>
  <view class="page">
    <view v-if="auth.isLoggedIn" class="welcome">
      <view class="avatar">👤</view>
      <view class="user-info">
        <view class="user-name">{{ auth.user?.displayName || auth.user?.email }}</view>
        <view class="user-email">{{ auth.user?.email }}</view>
      </view>
    </view>
    <view v-else class="welcome" @click="goLogin">
      <view class="avatar">👤</view>
      <view class="user-info">
        <view class="user-name">未登录</view>
        <view class="user-email">点击登录</view>
      </view>
    </view>

    <!-- 功能入口 -->
    <view class="menu">
      <view class="menu-item" @click="go('/pages/order/list')">
        <text class="menu-icon">📦</text>
        <text>我的订单</text>
      </view>
      <view class="menu-item" @click="goCart">
        <text class="menu-icon">🛒</text>
        <text>购物车</text>
      </view>
      <view class="menu-item" @click="goChat">
        <text class="menu-icon">💬</text>
        <text>在线客服</text>
      </view>
      <view class="menu-item" @click="go('/pages/index/index')">
        <text class="menu-icon">🏠</text>
        <text>回到首页</text>
      </view>
    </view>

    <view v-if="auth.isLoggedIn" class="logout-btn" @click="auth.logout()">退出登录</view>
  </view>
</template>

<script setup lang="ts">
import { onShow } from '@dcloudio/uni-app'
import { useAuthStore } from '../../stores/auth'

const auth = useAuthStore()

function goLogin() {
  uni.navigateTo({ url: '/pages/login/login' })
}

function go(url: string) {
  if (url === '/pages/index/index') {
    uni.switchTab({ url })
  } else {
    uni.navigateTo({ url })
  }
}

function goCart() {
  uni.switchTab({ url: '/pages/cart/cart' })
}

function goChat() {
  if (!auth.isLoggedIn) {
    uni.showToast({ title: '请先登录', icon: 'none' })
    setTimeout(() => goLogin(), 600)
    return
  }
  uni.navigateTo({ url: '/pages/im/chat' })
}

onShow(() => auth.restore())
</script>

<style scoped>
.welcome { display: flex; align-items: center; background: linear-gradient(135deg, #e64340, #ff7d6e); padding: 60rpx 40rpx; color: #fff; }
.avatar { width: 120rpx; height: 120rpx; border-radius: 50%; background: rgba(255,255,255,0.25); display: flex; align-items: center; justify-content: center; font-size: 60rpx; }
.user-info { margin-left: 24rpx; }
.user-name { font-size: 36rpx; font-weight: 500; }
.user-email { font-size: 24rpx; opacity: 0.9; margin-top: 8rpx; }
.menu { display: flex; flex-wrap: wrap; background: #fff; margin: 16rpx; border-radius: 12rpx; padding: 20rpx 0; }
.menu-item { width: 25%; display: flex; flex-direction: column; align-items: center; padding: 24rpx 0; }
.menu-icon { font-size: 48rpx; }
.menu-item text:last-child { font-size: 24rpx; color: #666; margin-top: 12rpx; }
.logout-btn { margin: 40rpx 16rpx; text-align: center; background: #fff; padding: 24rpx 0; border-radius: 12rpx; color: #e64340; }
</style>

import { defineStore } from 'pinia'
import { authApi } from '../api'

interface UserInfo {
  id: string
  email: string
  displayName: string
  roles: string[]
}

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: (uni.getStorageSync('token') as string) || '',
    user: null as UserInfo | null,
  }),
  getters: {
    isLoggedIn: (s) => !!s.token,
    userId: (s) => s.user?.id || '',
  },
  actions: {
    async login(email: string, password: string) {
      const res = await authApi.login({ email, password })
      this.token = res.token
      this.user = res.user
      uni.setStorageSync('token', res.token)
      uni.setStorageSync('user', res.user)
    },
    async register(email: string, password: string, displayName: string) {
      const res = await authApi.register({ email, password, displayName })
      this.token = res.token
      this.user = res.user
      uni.setStorageSync('token', res.token)
      uni.setStorageSync('user', res.user)
    },
    restore() {
      this.token = (uni.getStorageSync('token') as string) || ''
      this.user = (uni.getStorageSync('user') as UserInfo) || null
    },
    logout() {
      this.token = ''
      this.user = null
      uni.removeStorageSync('token')
      uni.removeStorageSync('user')
      uni.showToast({ title: '已退出登录', icon: 'none' })
      setTimeout(() => uni.reLaunch({ url: '/pages/index/index' }), 500)
    },
  },
})

import { defineStore } from 'pinia'
import { authApi, type AuthUser, type MerchantInfo } from '../api/auth'

// 桌面端认证/商户状态 store（Electron 环境 localStorage 由 Chromium 持久化）
export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('token') || '',
    user: null as AuthUser | null,
    merchant: null as MerchantInfo | null,
  }),
  getters: {
    isLoggedIn: (s) => !!s.token,
    displayName: (s) => s.user?.displayName || '',
    isAdmin: (s) => s.user?.roles?.includes('admin') ?? false,
    merchantId: (s) => s.merchant?.id || localStorage.getItem('merchantId') || '',
    merchantName: (s) => s.merchant?.name || '',
    isApproved: (s) => s.merchant?.status === 2,
  },
  actions: {
    setSession(token: string, user: AuthUser) {
      this.token = token
      this.user = user
      localStorage.setItem('token', token)
    },
    async fetchMerchant() {
      try {
        const data = await authApi.me()
        this.merchant = data
        if (data?.id) {
          localStorage.setItem('merchantId', data.id)
        }
        return data
      } catch {
        return null
      }
    },
    logout() {
      this.token = ''
      this.user = null
      this.merchant = null
      localStorage.removeItem('token')
      localStorage.removeItem('merchantId')
    },
  },
})

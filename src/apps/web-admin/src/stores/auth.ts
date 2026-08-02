import { defineStore } from 'pinia'

export interface AdminUser {
  id: string
  email: string
  displayName: string
  roles: string[]
}

// 平台管理后台认证 store（仅 admin 角色可访问）
export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('token') || '',
    user: null as AdminUser | null,
  }),
  getters: {
    isLoggedIn: (s) => !!s.token,
  },
  actions: {
    setSession(token: string, user: AdminUser) {
      this.token = token
      this.user = user
      localStorage.setItem('token', token)
    },
    logout() {
      this.token = ''
      this.user = null
      localStorage.removeItem('token')
    },
  },
})

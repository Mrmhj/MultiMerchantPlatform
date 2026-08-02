import { defineStore } from 'pinia'
import http from '../api/http'

export interface MerchantInfo {
  id: string
  ownerUserId: string
  name: string
  licenseNo: string
  contactName: string
  contactPhone: string
  contactEmail?: string
  description?: string
  status: number
  rejectReason?: string
  approvedAt?: string
  createdAt: string
}

// 商户端认证/商户状态 store
export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('token') || '',
    merchant: null as MerchantInfo | null,
  }),
  getters: {
    isLoggedIn: (s) => !!s.token,
    merchantId: (s) => s.merchant?.id || localStorage.getItem('merchantId') || '',
    isApproved: (s) => s.merchant?.status === 2,
    merchantStatus: (s) => s.merchant?.status ?? 0, // 1待审 2通过 3驳回 4禁用
  },
  actions: {
    setToken(token: string) {
      this.token = token
      localStorage.setItem('token', token)
    },
    async fetchMerchant() {
      try {
        const data = (await http.get('/merchant/merchants/me')) as MerchantInfo
        this.merchant = data
        if (data.id) {
          localStorage.setItem('merchantId', data.id)
        }
        return data
      } catch {
        return null
      }
    },
    logout() {
      this.token = ''
      this.merchant = null
      localStorage.removeItem('token')
      localStorage.removeItem('merchantId')
    },
  },
})

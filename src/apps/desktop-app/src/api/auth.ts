import http from './http'
import type { PagedResult } from './announcements'

// ── 认证 DTO ──────────────────────────────────────────────
export interface AuthUser {
  id: string
  email: string
  displayName: string
  roles: string[]
  status: number
}

export interface AuthResponse {
  token: string
  expiresAt: string
  user: AuthUser
}

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

export interface PagedMerchant extends PagedResult<MerchantInfo> {}

// ── 认证/商户 API（网关 /identity/**、/merchant/**）───────
export const authApi = {
  /** 登录（identity-service，签发 JWT） */
  login(data: { email: string; password: string }) {
    return http.post('/identity/auth/login', data) as Promise<AuthResponse>
  },

  /** 我的商户信息（未入驻返回 null） */
  me() {
    return http.get('/merchant/merchants/me') as Promise<MerchantInfo | null>
  },
}

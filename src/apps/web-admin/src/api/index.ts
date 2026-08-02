import http from './http'

// ==================== 通用类型 ====================
export interface AuthResponse {
  token: string
  expiresAt: string
  user: { id: string; email: string; displayName: string; roles: string[]; status: number }
}

// ==================== 认证（网关 /api/identity/**） ====================
export const authApi = {
  login: (data: { email: string; password: string }) =>
    http.post<AuthResponse>('/identity/auth/login', data),
}

// ==================== BI 分析（网关 /api/bi/**） ====================
export interface BiOverview {
  totalGmv: number
  totalOrders: number
  paidOrders: number
  completedOrders: number
  merchantCount: number
  productCount: number
  userCount: number
  syncedAt: string
}

export interface SalesTrendPoint {
  date: string
  gmv: number
  orderCount: number
}

export interface MerchantRankItem {
  merchantId: string
  merchantName: string
  gmv: number
  orderCount: number
}

export interface ProductRankItem {
  productId: string
  productName: string
  quantity: number
  amount: number
}

export interface OrderStatusItem {
  status: number
  count: number
}

export interface BiSyncResult {
  success: boolean
  error?: string
  dailySales: number
  merchantRows: number
  productRows: number
  statusRows: number
  merchantCount: number
  productCount: number
  userCount: number
  totalGmv: number
  totalOrders: number
  syncedAt: string
}

export const biApi = {
  overview: () => http.get<BiOverview>('/bi/overview'),
  salesTrend: (days = 30) => http.get<SalesTrendPoint[]>('/bi/sales-trend', { params: { days } }),
  merchantRank: (top = 10) => http.get<MerchantRankItem[]>('/bi/merchant-rank', { params: { top } }),
  productRank: (top = 10) => http.get<ProductRankItem[]>('/bi/product-rank', { params: { top } }),
  orderStatus: () => http.get<OrderStatusItem[]>('/bi/order-status'),
  sync: () => http.post<BiSyncResult>('/bi/sync', {}),
}

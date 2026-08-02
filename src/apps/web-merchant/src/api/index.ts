import http from './http'

// ==================== 通用类型 ====================
export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

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

// ==================== 商户入驻（网关 /api/merchant/**） ====================
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

export const merchantApi = {
  apply: (data: {
    name: string; licenseNo: string; contactName: string; contactPhone: string
    contactEmail?: string; description?: string
  }) => http.post<MerchantInfo>('/merchant/merchants/apply', data),
  me: () => http.get<MerchantInfo>('/merchant/merchants/me'),
}

// ==================== 商品（网关 /api/product/**） ====================
export interface SkuItem {
  id?: string
  skuCode: string
  spec: string
  price: number
  stock: number
  isActive?: boolean
}

export interface Product {
  id: string
  merchantId: string
  name: string
  categoryId: string
  description?: string
  coverImage?: string
  status: number
  skus: SkuItem[]
  createdAt: string
}

export interface Category {
  id: string
  name: string
  parentId?: string
  sortOrder: number
  isActive: boolean
}

export const productApi = {
  list: (params: { page?: number; pageSize?: number; status?: string; keyword?: string }) =>
    http.get<PagedResult<Product>>('/product/products', { params }),
  detail: (id: string) => http.get<Product>(`/product/products/${id}`),
  create: (data: { name: string; categoryId: string; description?: string; coverImage?: string; skus: SkuItem[] }) =>
    http.post<Product>('/product/products', data),
  update: (id: string, data: { name: string; categoryId: string; description?: string; coverImage?: string }) =>
    http.put<Product>(`/product/products/${id}`, data),
  updateStatus: (id: string, status: number) =>
    http.put<Product>(`/product/products/${id}/status`, { status }),
  categories: {
    list: () => http.get<Category[]>('/product/categories'),
    create: (data: { name: string; parentId?: string; sortOrder: number; isActive?: boolean }) =>
      http.post<Category>('/product/categories', data),
    update: (id: string, data: { name: string; parentId?: string; sortOrder: number; isActive?: boolean }) =>
      http.put<Category>(`/product/categories/${id}`, data),
    remove: (id: string) => http.delete<void>(`/product/categories/${id}`),
  },
}

// ==================== 订单（网关 /api/order/**） ====================
export interface OrderItem {
  id: string
  productId: string
  productName: string
  skuId: string
  skuCode: string
  spec: string
  unitPrice: number
  quantity: number
}

export interface SubOrder {
  id: string
  orderId: string
  merchantId: string
  merchantName: string
  totalAmount: number
  status: number
  carrierCode?: string
  trackingNo?: string
  items: OrderItem[]
  createdAt?: string
}

export const orderApi = {
  merchantList: (params: { page?: number; pageSize?: number; status?: string }) =>
    http.get<PagedResult<SubOrder>>('/order/orders/merchant', { params }),
  merchantDetail: (id: string) => http.get<SubOrder>(`/order/orders/merchant/${id}`),
  ship: (id: string, data: { carrierCode: string; trackingNo: string }) =>
    http.post<SubOrder>(`/order/orders/merchant/${id}/ship`, data),
  complete: (id: string) => http.post<SubOrder>(`/order/orders/merchant/${id}/complete`),
}

// ==================== 库存（网关 /api/stock/**） ====================
export interface StockInfo {
  skuId: string
  merchantId: string
  total: number
  reserved: number
  available: number
  skuCode?: string
  spec?: string
}

export interface StockTransaction {
  type: number
  quantity: number
  referenceId?: string
  createdAt: string
}

export const stockApi = {
  list: (params: { page?: number; pageSize?: number }) =>
    http.get<PagedResult<StockInfo>>('/stock/stocks', { params }),
  detail: (skuId: string) => http.get<StockInfo>(`/stock/stocks/${skuId}`),
  increase: (skuId: string, quantity: number) =>
    http.post<StockInfo>(`/stock/stocks/${skuId}/increase`, { quantity }),
  transactions: (skuId: string) =>
    http.get<StockTransaction[]>(`/stock/stocks/${skuId}/transactions`),
}

// ==================== 营销（网关 /api/promotion/**） ====================
export interface PromotionActivity {
  id: string
  merchantId: string
  name: string
  thresholdAmount: number
  discountAmount: number
  startTime: string
  endTime: string
  status: string
}

export interface Coupon {
  id: string
  merchantId: string
  name: string
  thresholdAmount: number
  discountAmount: number
  totalQuantity: number
  limitPerUser: number
  validFrom: string
  validUntil: string
  status: string
  claimedCount?: number
}

export const promotionApi = {
  activities: {
    list: (params: { page?: number; pageSize?: number; status?: string }) =>
      http.get<PagedResult<PromotionActivity>>('/promotion/activities', { params }),
    detail: (id: string) => http.get<PromotionActivity>(`/promotion/activities/${id}`),
    create: (data: { name: string; thresholdAmount: number; discountAmount: number; startTime: string; endTime: string }) =>
      http.post<PromotionActivity>('/promotion/activities', data),
    updateStatus: (id: string, active: boolean) =>
      http.put<PromotionActivity>(`/promotion/activities/${id}/status`, { active }),
  },
  coupons: {
    list: (params: { page?: number; pageSize?: number; status?: string }) =>
      http.get<PagedResult<Coupon>>('/promotion/coupons', { params }),
    detail: (id: string) => http.get<Coupon>(`/promotion/coupons/${id}`),
    create: (data: { name: string; thresholdAmount: number; discountAmount: number; totalQuantity: number; limitPerUser: number; validFrom: string; validUntil: string }) =>
      http.post<Coupon>('/promotion/coupons', data),
    updateStatus: (id: string, active: boolean) =>
      http.put<Coupon>(`/promotion/coupons/${id}/status`, { active }),
  },
}

// ==================== 评价（网关 /api/reviews/**） ====================
export interface Review {
  id: string
  productId: string
  productName: string
  skuSpec: string
  rating: number
  content: string
  isAnonymous: boolean
  displayName: string
  status: string
  replyContent?: string
  repliedAt?: string
  createdAt: string
}

export const reviewApi = {
  merchantList: (params: { page?: number; pageSize?: number; productId?: string; rating?: number; status?: string }) =>
    http.get<PagedResult<Review>>('/reviews/merchant', { params }),
  reply: (id: string, reply: string) =>
    http.put<Review>(`/reviews/${id}/reply`, { reply }),
  changeStatus: (id: string, visible: boolean) =>
    http.put<Review>(`/reviews/${id}/status`, { visible }),
}

// ==================== 物流（网关 /api/logistics/**） ====================
export interface Track {
  status: number
  description: string
  location?: string
  trackedAt: string
}

export interface Shipment {
  id: string
  merchantId: string
  subOrderId: string
  orderNo: string
  carrierCode: string
  carrierName: string
  trackingNo: string
  status: number
  signedAt?: string
  tracks: Track[]
  createdAt: string
}

export interface LogisticsCompany {
  id: string
  code: string
  name: string
  trackingUrlTemplate?: string
  isEnabled: boolean
}

export const logisticsApi = {
  shipments: (params: { page?: number; pageSize?: number; status?: string }) =>
    http.get<PagedResult<Shipment>>('/logistics/shipments/merchant', { params }),
  detail: (id: string) => http.get<Shipment>(`/logistics/shipments/merchant/${id}`),
  companies: () => http.get<LogisticsCompany[]>('/logistics/shipments/merchant/companies'),
}

// ==================== 结算（网关 /api/settlements/**） ====================
export interface SettlementItem {
  subOrderId: string
  orderNo: string
  productAmount: number
  commissionAmount: number
  settleAmount: number
}

export interface Settlement {
  id: string
  merchantId: string
  merchantName: string
  cycleStart: string
  cycleEnd: string
  totalOrderAmount: number
  totalCommission: number
  settlementAmount: number
  status: string
  settledAt?: string
  paidAt?: string
  items: SettlementItem[]
  createdAt: string
}

export interface SettlementSummary {
  pendingCount: number
  settledCount: number
  paidCount: number
  pendingAmount: number
  settledAmount: number
  totalCommission: number
}

export const settlementApi = {
  list: (params: { page?: number; pageSize?: number; status?: string }) =>
    http.get<PagedResult<Settlement>>('/settlements/merchant', { params }),
  detail: (id: string) => http.get<Settlement>(`/settlements/merchant/${id}`),
  summary: () => http.get<SettlementSummary>('/settlements/merchant/summary'),
  commission: () => http.get<{ merchantId: string; rate: number; isDefault: boolean }>('/settlements/merchant/commission'),
}

// ==================== IM（网关 /api/im/**） ====================
export interface SessionMember {
  userId: string
  userName: string
  role: number
}

export interface ChatSession {
  id: string
  merchantId: string
  type: number
  name: string
  status: number
  lastMessageAt?: string
  lastMessagePreview?: string
  unreadCount: number
  members: SessionMember[]
  createdAt: string
}

export interface ChatMessage {
  id: string
  sessionId: string
  senderId: string
  senderName: string
  senderRole: number
  messageType: number
  content: string
  isRead: boolean
  readAt?: string
  createdAt: string
}

export const imApi = {
  merchantSessions: () => http.get<ChatSession[]>('/im/merchant/sessions'),
  messages: (sessionId: string, params: { beforeId?: string; limit?: number }) =>
    http.get<{ items: ChatMessage[]; hasMore: boolean }>(`/im/merchant/sessions/${sessionId}/messages`, { params }),
  markRead: (sessionId: string) =>
    http.post<{ sessionId: string; markedCount: number }>(`/im/merchant/sessions/${sessionId}/read`, {}),
  reply: (sessionId: string, content: string, messageType = 1) =>
    http.post<ChatMessage>(`/im/merchant/sessions/${sessionId}/reply`, { content, messageType }),
  createGroup: (data: { name: string; staffUserIds: string[] }) =>
    http.post<ChatSession>('/im/merchant/groups', data),
}

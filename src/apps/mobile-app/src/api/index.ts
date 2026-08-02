import { http } from './http'

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
  register: (data: { email: string; password: string; displayName?: string }) =>
    http.post<AuthResponse>('/identity/auth/register', data),
  login: (data: { email: string; password: string }) =>
    http.post<AuthResponse>('/identity/auth/login', data),
}

// ==================== 商品（C 端公开接口，网关 /api/product/**） ====================
export interface SkuInfo {
  id: string
  skuCode: string
  spec: string
  price: number
  stock: number
  isActive: boolean
}

export interface Product {
  id: string
  merchantId: string
  merchantName?: string
  name: string
  categoryId: string
  description?: string
  coverImage?: string
  status: number
  skus: SkuInfo[]
  createdAt: string
}

export const productApi = {
  list: (page = 1, pageSize = 12) =>
    http.get<PagedResult<Product>>('/product/products/public', { page, pageSize }),
  detail: (id: string) => http.get<Product>(`/product/products/public/${id}`),
}

// ==================== 搜索（网关 /api/search/**） ====================
export interface SearchItem {
  id: string
  name: string
  coverImage?: string
  price: number
  merchantId: string
}

export const searchApi = {
  products: (params: { keyword?: string; page?: number; pageSize?: number }) =>
    http.get<PagedResult<SearchItem>>('/search/products', params),
}

// ==================== 购物车（网关 /api/cart/**，买家） ====================
export interface CartItem {
  id: string
  skuId: string
  productId: string
  productName: string
  coverImage?: string
  skuCode: string
  spec: string
  price: number
  quantity: number
  isSelected: boolean
  merchantId: string
  merchantName: string
}

export interface Cart {
  items: CartItem[]
  totalSelectedCount: number
  totalSelectedAmount: number
}

export const cartApi = {
  list: () => http.get<Cart>('/cart'),
  add: (data: {
    merchantId: string
    merchantName: string
    productId: string
    productName: string
    skuId: string
    skuCode: string
    spec?: string
    unitPrice: number
    quantity: number
  }) => http.post<CartItem>('/cart/items', data),
  updateQuantity: (id: string, quantity: number) =>
    http.put<CartItem>(`/cart/items/${id}/quantity`, { quantity }),
  select: (id: string, isSelected: boolean) =>
    http.put<CartItem>(`/cart/items/${id}/select`, { isSelected }),
  remove: (id: string) => http.delete<void>(`/cart/items/${id}`),
}

// ==================== 订单（网关 /api/order/**，买家） ====================
export interface OrderItem {
  merchantId: string
  merchantName: string
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
  items: Array<OrderItem & { id: string; subtotal: number }>
}

export interface Order {
  id: string
  orderNo: string
  buyerUserId: string
  totalAmount: number
  status: number
  remark?: string
  subOrders: SubOrder[]
  createdAt: string
}

export const orderApi = {
  create: (items: OrderItem[], remark?: string) =>
    http.post<Order>('/order/orders', { items, remark }),
  list: (page = 1, pageSize = 10) =>
    http.get<PagedResult<Order>>('/order/orders', { page, pageSize }),
  detail: (id: string) => http.get<Order>(`/order/orders/${id}`),
  cancel: (id: string) => http.post<Order>(`/order/orders/${id}/cancel`),
}

// ==================== 支付（网关 /api/pay/**） ====================
export interface Payment {
  id: string
  payNo: string
  orderId: string
  amount: number
  status: number
  paidAt?: string
}

export const payApi = {
  create: (orderId: string, amount: number) =>
    http.post<Payment>('/pay/payments', { orderId, amount }),
  simulatePay: (id: string) => http.post<Payment>(`/pay/payments/${id}/simulate-pay`),
}

// ==================== IM（网关 /api/im/**，买家） ====================
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
  sessions: () => http.get<ChatSession[]>('/im/sessions'),
  createPrivate: (merchantId: string, peerUserId: string) =>
    http.post<ChatSession>('/im/sessions/private', { merchantId, peerUserId }),
  messages: (sessionId: string, params: { beforeId?: string; limit?: number }) =>
    http.get<{ items: ChatMessage[]; hasMore: boolean }>(`/im/sessions/${sessionId}/messages`, params),
  markRead: (sessionId: string) =>
    http.post<{ sessionId: string; markedCount: number }>(`/im/sessions/${sessionId}/read`, {}),
  send: (sessionId: string, content: string, messageType = 1) =>
    http.post<ChatMessage>(`/im/sessions/${sessionId}/send`, { content, messageType }),
}

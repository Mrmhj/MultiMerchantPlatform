import http from './http'

// ---- 类型定义 ----
export interface UserInfo {
  id: string
  email: string
  displayName: string
  roles: string[]
  status: number
}

export interface AuthResponse {
  token: string
  expiresAt: string
  user: UserInfo
}

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
  name: string
  categoryId: string
  description?: string
  coverImage?: string
  status: number
  skus: SkuInfo[]
  createdAt: string
}

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

export interface Payment {
  id: string
  payNo: string
  orderId: string
  amount: number
  status: number
  paidAt?: string
}

// ---- 认证（identity-service 经网关 /api/identity/**）----
export const authApi = {
  register: (data: { email: string; password: string; displayName?: string }) =>
    http.post<AuthResponse>('/identity/auth/register', data),
  login: (data: { email: string; password: string }) =>
    http.post<AuthResponse>('/identity/auth/login', data),
  me: () => http.get<UserInfo>('/identity/users/me'),
}

// ---- 商品（product-service 经网关 /api/product/**）----
export const productApi = {
  list: (page = 1, pageSize = 12) =>
    http.get<{ items: Product[]; totalCount: number; page: number; pageSize: number }>(
      `/product/products/public?page=${page}&pageSize=${pageSize}`,
    ),
  detail: (id: string) => http.get<Product>(`/product/products/public/${id}`),
}

// ---- 订单（order-service 经网关 /api/order/**）----
export const orderApi = {
  create: (items: OrderItem[], remark?: string) =>
    http.post<Order>('/order/orders', { items, remark }),
  list: (page = 1, pageSize = 10) =>
    http.get<{ items: Order[]; totalCount: number }>(`/order/orders?page=${page}&pageSize=${pageSize}`),
  detail: (id: string) => http.get<Order>(`/order/orders/${id}`),
  cancel: (id: string) => http.post<Order>(`/order/orders/${id}/cancel`),
}

// ---- 支付（pay-service 经网关 /api/pay/**）----
export const payApi = {
  create: (orderId: string, amount: number) =>
    http.post<Payment>('/pay/payments', { orderId, amount }),
  simulatePay: (id: string) => http.post<Payment>(`/pay/payments/${id}/simulate-pay`),
}

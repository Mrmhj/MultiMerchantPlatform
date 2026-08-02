// 请求封装：uni.request 统一处理（JWT 注入 + baseURL + 401/错误提示）
// 开发环境 H5 走 Vite 代理（/api → 网关 8000）；App 端走直连网关（BASE_URL 可配）

const BASE_URL = import.meta.env.VITE_API_BASE || '/api'

interface RequestOptions {
  url: string
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  data?: Record<string, unknown> | string | ArrayBuffer
  header?: Record<string, string>
}

export function request<T>(options: RequestOptions): Promise<T> {
  return new Promise((resolve, reject) => {
    const token = uni.getStorageSync('token')
    const header: Record<string, string> = {
      'Content-Type': 'application/json',
      ...(options.header || {}),
    }
    if (token) {
      header.Authorization = `Bearer ${token}`
    }

    uni.request({
      url: `${BASE_URL}${options.url}`,
      method: options.method || 'GET',
      data: options.data,
      header,
      success: (res) => {
        const status = res.statusCode
        if (status >= 200 && status < 300) {
          resolve(res.data as T)
        } else if (status === 401) {
          uni.removeStorageSync('token')
          uni.showToast({ title: '请先登录', icon: 'none' })
          setTimeout(() => {
            uni.navigateTo({ url: '/pages/login/login' })
          }, 600)
          reject(res.data)
        } else {
          const data = res.data as { error?: string } | undefined
          uni.showToast({ title: data?.error || `请求失败(${status})`, icon: 'none' })
          reject(res.data)
        }
      },
      fail: (err) => {
        uni.showToast({ title: '网络异常，请检查服务是否启动', icon: 'none' })
        reject(err)
      },
    })
  })
}

export const http = {
  get: <T>(url: string, params?: Record<string, unknown>) => {
    const query = params
      ? `?${Object.entries(params)
          .filter(([, v]) => v !== undefined && v !== null && v !== '')
          .map(([k, v]) => `${k}=${encodeURIComponent(String(v))}`)
          .join('&')}`
      : ''
    return request<T>({ url: `${url}${query}`, method: 'GET' })
  },
  post: <T>(url: string, data?: Record<string, unknown>) => request<T>({ url, method: 'POST', data }),
  put: <T>(url: string, data?: Record<string, unknown>) => request<T>({ url, method: 'PUT', data }),
  delete: <T>(url: string) => request<T>({ url, method: 'DELETE' }),
}

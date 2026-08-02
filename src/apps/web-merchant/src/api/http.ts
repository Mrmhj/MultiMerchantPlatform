import axios from 'axios'
import { ElMessage } from 'element-plus'
import router from '../router'

// Axios 统一封装：开发环境经 Vite 代理到 YARP 网关（8000）
// 商户端所有业务请求自动携带 JWT + X-Merchant-Id 请求头
const http = axios.create({
  baseURL: '/api',
  timeout: 20000,
})

// 请求拦截：注入 JWT + 商户 ID
http.interceptors.request.use((config) => {
  const token = localStorage.getItem('token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  const merchantId = localStorage.getItem('merchantId')
  if (merchantId) {
    config.headers['X-Merchant-Id'] = merchantId
  }
  return config
})

// 响应拦截：统一错误处理 + 401 跳登录
http.interceptors.response.use(
  (response) => response.data,
  (error) => {
    const status = error.response?.status
    const message = error.response?.data?.error || error.message || '请求失败'
    if (status === 401) {
      localStorage.removeItem('token')
      localStorage.removeItem('merchantId')
      ElMessage.warning('请先登录')
      router.push({ name: 'login' })
    } else {
      ElMessage.error(message)
    }
    return Promise.reject(error)
  },
)

export default http

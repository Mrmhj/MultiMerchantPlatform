import { createRouter, createWebHashHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

// 桌面端使用 hash 路由（Electron file:// 协议下 history 模式不可用）
const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/login', name: 'login', component: () => import('../views/Login.vue') },
    {
      path: '/',
      component: () => import('../views/layout/MainLayout.vue'),
      redirect: '/dashboard',
      children: [
        { path: 'dashboard', name: 'dashboard', component: () => import('../views/Dashboard.vue') },
        { path: 'announcements', name: 'announcements', component: () => import('../views/announcements/List.vue') },
        { path: 'announcements/:id', name: 'announcement-detail', component: () => import('../views/announcements/Detail.vue') },
        { path: 'emails', name: 'emails', component: () => import('../views/emails/Inbox.vue') },
        { path: 'emails/compose', name: 'email-compose', component: () => import('../views/emails/Compose.vue') },
        { path: 'notifications', name: 'notifications', component: () => import('../views/notifications/Inbox.vue') },
      ],
    },
  ],
})

// 登录守卫：未登录一律跳登录页
router.beforeEach((to) => {
  const auth = useAuthStore()
  if (to.name !== 'login' && !auth.isLoggedIn) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
  if (to.name === 'login' && auth.isLoggedIn) {
    return { name: 'dashboard' }
  }
  return true
})

export default router

import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'home', component: () => import('../views/Home.vue') },
    { path: '/product/:id', name: 'product-detail', component: () => import('../views/ProductDetail.vue') },
    { path: '/login', name: 'login', component: () => import('../views/Login.vue') },
    { path: '/register', name: 'register', component: () => import('../views/Register.vue') },
    { path: '/order/submit', name: 'order-submit', component: () => import('../views/OrderSubmit.vue') },
    { path: '/orders', name: 'orders', component: () => import('../views/Orders.vue') },
    { path: '/orders/:id', name: 'order-detail', component: () => import('../views/OrderDetail.vue') },
  ],
})

// 简单登录守卫：下单/订单页需登录
router.beforeEach((to) => {
  const token = localStorage.getItem('token')
  const needAuth = ['order-submit', 'orders', 'order-detail'].includes(to.name as string)
  if (needAuth && !token) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
  return true
})

export default router

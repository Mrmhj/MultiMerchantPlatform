import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', name: 'login', component: () => import('../views/Login.vue') },
    {
      path: '/',
      component: () => import('../layouts/MerchantLayout.vue'),
      redirect: '/dashboard',
      children: [
        { path: 'dashboard', name: 'dashboard', component: () => import('../views/Dashboard.vue') },
        { path: 'apply', name: 'merchant-apply', component: () => import('../views/MerchantApply.vue') },
        { path: 'products', name: 'products', component: () => import('../views/products/Products.vue') },
        { path: 'products/edit/:id?', name: 'product-edit', component: () => import('../views/products/ProductEdit.vue') },
        { path: 'categories', name: 'categories', component: () => import('../views/products/Categories.vue') },
        { path: 'orders', name: 'orders', component: () => import('../views/orders/Orders.vue') },
        { path: 'orders/:id', name: 'order-detail', component: () => import('../views/orders/OrderDetail.vue') },
        { path: 'stocks', name: 'stocks', component: () => import('../views/stocks/Stocks.vue') },
        { path: 'marketing', name: 'marketing', component: () => import('../views/marketing/Promotions.vue') },
        { path: 'reviews', name: 'reviews', component: () => import('../views/reviews/Reviews.vue') },
        { path: 'shipments', name: 'shipments', component: () => import('../views/logistics/Shipments.vue') },
        { path: 'settlements', name: 'settlements', component: () => import('../views/settlements/Settlements.vue') },
        { path: 'im', name: 'im', component: () => import('../views/im/ImChat.vue') },
      ],
    },
  ],
})

// 登录守卫：未登录一律跳登录页
router.beforeEach(async (to) => {
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

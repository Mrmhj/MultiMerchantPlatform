<template>
  <view class="page">
    <!-- 搜索栏 -->
    <view class="search-bar">
      <input class="search-input" v-model="keyword" placeholder="搜索商品" confirm-type="search"
             @confirm="doSearch" />
      <view class="search-btn" @click="doSearch">搜索</view>
    </view>

    <!-- 分类入口（骨架静态展示） -->
    <view class="cat-row">
      <view v-for="c in categories" :key="c.name" class="cat-item" @click="searchByCat(c.keyword)">
        <text class="cat-icon">{{ c.icon }}</text>
        <text class="cat-name">{{ c.name }}</text>
      </view>
    </view>

    <!-- 商品列表 -->
    <view class="product-grid">
      <view v-for="p in list" :key="p.id" class="product-card" @click="goDetail(p.id)">
        <image v-if="p.coverImage" class="product-img" :src="p.coverImage" mode="aspectFill" />
        <view v-else class="product-img placeholder">无图</view>
        <view class="product-info">
          <view class="product-name">{{ p.name }}</view>
          <view class="product-price">¥ {{ minPrice(p) }}</view>
        </view>
      </view>
    </view>
    <view v-if="loading" class="load-tip">加载中…</view>
    <view v-else-if="finished" class="load-tip">没有更多了</view>

    <view class="footer-bar">
      <view class="footer-item" @click="goTab('/pages/index/index')">首页</view>
      <view class="footer-item" @click="goTab('/pages/cart/cart')">购物车</view>
      <view class="footer-item" @click="goTab('/pages/order/list')">订单</view>
      <view class="footer-item" @click="goTab('/pages/profile/profile')">我的</view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { onLoad, onReachBottom, onPullDownRefresh } from '@dcloudio/uni-app'
import { ref } from 'vue'
import { productApi, searchApi, type Product } from '../../api'

const keyword = ref('')
const list = ref<Product[]>([])
const page = ref(1)
const loading = ref(false)
const finished = ref(false)

const categories = [
  { name: '全部', icon: '🏠', keyword: '' },
  { name: '食品', icon: '🍞', keyword: '面包' },
  { name: '数码', icon: '📱', keyword: '手机' },
  { name: '服饰', icon: '👕', keyword: '服饰' },
  { name: '家居', icon: '🛋', keyword: '家居' },
]

function minPrice(p: Product) {
  if (!p.skus?.length) return '0.00'
  return Math.min(...p.skus.filter((s) => s.isActive).map((s) => s.price)).toFixed(2)
}

async function load(reset = false) {
  if (loading.value || (finished.value && !reset)) return
  loading.value = true
  try {
    const p = reset ? 1 : page.value
    const res = keyword.value
      ? await searchApi.products({ keyword: keyword.value, page: p, pageSize: 12 })
      : await productApi.list(p, 12)
    const items = (res.items as unknown as Product[])
    list.value = reset ? items : [...list.value, ...items]
    page.value = p + 1
    finished.value = list.value.length >= res.totalCount
  } finally {
    loading.value = false
  }
}

function doSearch() {
  list.value = []
  page.value = 1
  finished.value = false
  load(true)
}

function searchByCat(kw: string) {
  keyword.value = kw
  doSearch()
}

function goDetail(id: string) {
  uni.navigateTo({ url: `/pages/product/detail?id=${id}` })
}

function goTab(url: string) {
  uni.switchTab({ url })
}

onLoad(() => load(true))
onReachBottom(() => load())
onPullDownRefresh(async () => {
  await load(true)
  uni.stopPullDownRefresh()
})
</script>

<style scoped>
.page { padding-bottom: 120rpx; }
.search-bar { display: flex; padding: 20rpx; background: #fff; }
.search-input { flex: 1; height: 68rpx; background: #f5f6f7; border-radius: 34rpx; padding: 0 30rpx; font-size: 28rpx; }
.search-btn { margin-left: 16rpx; color: #e64340; font-size: 28rpx; line-height: 68rpx; }
.cat-row { display: flex; background: #fff; padding: 20rpx 0; margin-top: 16rpx; }
.cat-item { flex: 1; display: flex; flex-direction: column; align-items: center; }
.cat-icon { font-size: 44rpx; }
.cat-name { font-size: 24rpx; color: #666; margin-top: 8rpx; }
.product-grid { display: flex; flex-wrap: wrap; padding: 8rpx; }
.product-card { width: 48.8%; margin: 8rpx; background: #fff; border-radius: 12rpx; overflow: hidden; }
.product-img { width: 100%; height: 320rpx; }
.placeholder { display: flex; align-items: center; justify-content: center; color: #bbb; background: #f5f6f7; }
.product-info { padding: 16rpx; }
.product-name { font-size: 26rpx; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
.product-price { color: #e64340; font-size: 30rpx; font-weight: 500; margin-top: 8rpx; }
.load-tip { text-align: center; color: #999; font-size: 24rpx; padding: 20rpx; }
.footer-bar { position: fixed; bottom: 0; left: 0; right: 0; display: flex; background: #fff; border-top: 1px solid #eee; }
.footer-item { flex: 1; text-align: center; padding: 18rpx 0; font-size: 26rpx; color: #666; }
</style>

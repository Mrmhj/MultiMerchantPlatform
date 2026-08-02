<template>
  <view class="page" v-if="product">
    <!-- 商品图 -->
    <image v-if="product.coverImage" class="hero" :src="product.coverImage" mode="aspectFill" />
    <view v-else class="hero placeholder">暂无图片</view>

    <!-- 信息 -->
    <view class="info-card">
      <view class="price">¥ {{ currentSku ? currentSku.price.toFixed(2) : '—' }}</view>
      <view class="name">{{ product.name }}</view>
      <view class="desc">{{ product.description || '暂无描述' }}</view>
    </view>

    <!-- SKU 选择 -->
    <view class="info-card">
      <view class="section-title">选择规格</view>
      <view class="sku-row">
        <view v-for="s in activeSkus" :key="s.id" class="sku-item" :class="{ active: currentSku?.id === s.id }"
              @click="currentSku = s">
          {{ s.spec }}
        </view>
      </view>
      <view class="qty-row">
        <text>数量</text>
        <view class="stepper">
          <view class="step-btn" @click="changeQty(-1)">−</view>
          <text class="step-num">{{ qty }}</text>
          <view class="step-btn" @click="changeQty(1)">＋</view>
        </view>
      </view>
    </view>

    <!-- 操作栏 -->
    <view class="action-bar">
      <view class="action-btn plain" @click="addCart">加入购物车</view>
      <view class="action-btn primary" @click="buyNow">立即购买</view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { onLoad } from '@dcloudio/uni-app'
import { cartApi, orderApi, payApi, productApi, type Product, type SkuInfo } from '../../api'

const product = ref<Product | null>(null)
const currentSku = ref<SkuInfo | null>(null)
const qty = ref(1)

const activeSkus = computed(() => product.value?.skus?.filter((s) => s.isActive) || [])

function changeQty(delta: number) {
  qty.value = Math.max(1, Math.min(qty.value + delta, currentSku.value?.stock || 99))
}

function ensureLogin(): boolean {
  if (uni.getStorageSync('token')) return true
  uni.showToast({ title: '请先登录', icon: 'none' })
  setTimeout(() => uni.navigateTo({ url: '/pages/login/login' }), 600)
  return false
}

async function addCart() {
  if (!product.value || !currentSku.value) {
    uni.showToast({ title: '请选择规格', icon: 'none' })
    return
  }
  if (!ensureLogin()) return
  await cartApi.add({
    merchantId: product.value.merchantId,
    merchantName: product.value.merchantName || '商户',
    productId: product.value.id,
    productName: product.value.name,
    skuId: currentSku.value.id,
    skuCode: currentSku.value.skuCode,
    spec: currentSku.value.spec,
    unitPrice: currentSku.value.price,
    quantity: qty.value,
  })
  uni.showToast({ title: '已加入购物车', icon: 'success' })
}

async function buyNow() {
  if (!product.value || !currentSku.value) {
    uni.showToast({ title: '请选择规格', icon: 'none' })
    return
  }
  if (!ensureLogin()) return
  const order = await orderApi.create([{
    merchantId: product.value.merchantId,
    merchantName: '',
    productId: product.value.id,
    productName: product.value.name,
    skuId: currentSku.value.id,
    skuCode: currentSku.value.skuCode,
    spec: currentSku.value.spec,
    unitPrice: currentSku.value.price,
    quantity: qty.value,
  }])
  uni.navigateTo({ url: `/pages/order/detail?id=${order.id}&justCreated=1` })
}

onLoad(async (query) => {
  const id = query?.id as string
  product.value = await productApi.detail(id)
  currentSku.value = activeSkus.value[0] || null
})
</script>

<style scoped>
.page { padding-bottom: 140rpx; }
.hero { width: 100%; height: 560rpx; }
.placeholder { display: flex; align-items: center; justify-content: center; color: #bbb; background: #f5f6f7; }
.info-card { background: #fff; padding: 24rpx; margin-top: 16rpx; }
.price { color: #e64340; font-size: 40rpx; font-weight: 500; }
.name { font-size: 32rpx; margin-top: 8rpx; }
.desc { font-size: 26rpx; color: #999; margin-top: 8rpx; }
.section-title { font-size: 28rpx; font-weight: 500; margin-bottom: 16rpx; }
.sku-row { display: flex; flex-wrap: wrap; }
.sku-item { padding: 12rpx 30rpx; border: 1px solid #ddd; border-radius: 8rpx; margin: 0 16rpx 16rpx 0; font-size: 26rpx; }
.sku-item.active { border-color: #e64340; color: #e64340; background: #fff3f3; }
.qty-row { display: flex; justify-content: space-between; align-items: center; margin-top: 16rpx; }
.stepper { display: flex; align-items: center; }
.step-btn { width: 60rpx; height: 60rpx; background: #f5f6f7; text-align: center; line-height: 60rpx; font-size: 32rpx; }
.step-num { width: 80rpx; text-align: center; }
.action-bar { position: fixed; bottom: 0; left: 0; right: 0; display: flex; background: #fff; padding: 16rpx 24rpx; padding-bottom: calc(16rpx + env(safe-area-inset-bottom)); }
.action-btn { flex: 1; text-align: center; padding: 22rpx 0; border-radius: 40rpx; font-size: 30rpx; }
.action-btn.plain { background: #ffeceb; color: #e64340; margin-right: 16rpx; }
.action-btn.primary { background: #e64340; color: #fff; }
</style>

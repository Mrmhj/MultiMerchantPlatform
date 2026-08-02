<template>
  <view class="page">
    <view v-if="loading" class="tip">加载中…</view>
    <view v-else-if="!cart || cart.items.length === 0" class="empty">
      <text>购物车是空的</text>
      <view class="go-btn" @click="goShopping">去逛逛</view>
    </view>

    <template v-else>
      <!-- 购物车条目（按商户分组） -->
      <view v-for="group in grouped" :key="group.merchantId" class="merchant-group">
        <view class="merchant-name">🏬 {{ group.merchantName }}</view>
        <view v-for="item in group.items" :key="item.id" class="cart-item">
          <view class="check" :class="{ checked: item.isSelected }" @click="toggleSelect(item)">✓</view>
          <image v-if="item.coverImage" class="item-img" :src="item.coverImage" mode="aspectFill" />
          <view v-else class="item-img placeholder">无图</view>
          <view class="item-info">
            <view class="item-name">{{ item.productName }}</view>
            <view class="item-spec">{{ item.spec }}</view>
            <view class="item-bottom">
              <text class="item-price">¥ {{ item.price.toFixed(2) }}</text>
              <view class="stepper">
                <view class="step-btn" @click="changeQty(item, -1)">−</view>
                <text class="step-num">{{ item.quantity }}</text>
                <view class="step-btn" @click="changeQty(item, 1)">＋</view>
              </view>
            </view>
          </view>
          <view class="remove" @click="removeItem(item)">✕</view>
        </view>
      </view>

      <!-- 结算栏 -->
      <view class="checkout-bar">
        <view class="check all" :class="{ checked: allSelected }" @click="toggleAll">✓</view>
        <text class="all-label">全选</text>
        <view class="total">
          <text>合计：</text>
          <text class="total-price">¥ {{ cart.totalSelectedAmount.toFixed(2) }}</text>
        </view>
        <view class="checkout-btn" @click="checkout">去结算</view>
      </view>
    </template>
  </view>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { onShow } from '@dcloudio/uni-app'
import { cartApi, orderApi, payApi, type Cart, type CartItem } from '../../api'

const cart = ref<Cart | null>(null)
const loading = ref(false)

const grouped = computed(() => {
  if (!cart.value) return []
  const map = new Map<string, CartItem[]>()
  for (const item of cart.value.items) {
    const list = map.get(item.merchantId) || []
    list.push(item)
    map.set(item.merchantId, list)
  }
  return Array.from(map.entries()).map(([merchantId, items]) => ({
    merchantId,
    merchantName: items[0].merchantName,
    items,
  }))
})

const allSelected = computed(() => {
  if (!cart.value?.items.length) return false
  return cart.value.items.every((i) => i.isSelected)
})

async function load() {
  loading.value = true
  try {
    cart.value = await cartApi.list()
  } finally {
    loading.value = false
  }
}

async function toggleSelect(item: CartItem) {
  await cartApi.select(item.id, !item.isSelected)
  load()
}

async function toggleAll() {
  const target = !allSelected.value
  for (const item of cart.value?.items || []) {
    if (item.isSelected !== target) {
      await cartApi.select(item.id, target)
    }
  }
  load()
}

async function changeQty(item: CartItem, delta: number) {
  const q = Math.max(1, item.quantity + delta)
  await cartApi.updateQuantity(item.id, q)
  load()
}

async function removeItem(item: CartItem) {
  uni.showModal({
    title: '提示',
    content: '确认删除该商品？',
    success: async (res) => {
      if (res.confirm) {
        await cartApi.remove(item.id)
        load()
      }
    },
  })
}

async function checkout() {
  const selected = cart.value?.items.filter((i) => i.isSelected) || []
  if (selected.length === 0) {
    uni.showToast({ title: '请先选择商品', icon: 'none' })
    return
  }
  const order = await orderApi.create(selected.map((i) => ({
    merchantId: i.merchantId,
    merchantName: i.merchantName,
    productId: i.productId,
    productName: i.productName,
    skuId: i.skuId,
    skuCode: i.skuCode,
    spec: i.spec,
    unitPrice: i.price,
    quantity: i.quantity,
  })))
  uni.navigateTo({ url: `/pages/order/detail?id=${order.id}&justCreated=1` })
}

function goShopping() {
  uni.switchTab({ url: '/pages/index/index' })
}

onShow(() => {
  if (uni.getStorageSync('token')) {
    load()
  } else {
    cart.value = { items: [], totalSelectedCount: 0, totalSelectedAmount: 0 }
  }
})
</script>

<style scoped>
.page { padding-bottom: 140rpx; }
.tip { text-align: center; padding: 60rpx; color: #999; }
.empty { display: flex; flex-direction: column; align-items: center; padding: 120rpx 0; color: #999; }
.go-btn { margin-top: 24rpx; padding: 14rpx 60rpx; background: #e64340; color: #fff; border-radius: 40rpx; }
.merchant-group { background: #fff; margin: 16rpx; border-radius: 12rpx; }
.merchant-name { padding: 20rpx 24rpx; font-weight: 500; border-bottom: 1px solid #f5f6f7; }
.cart-item { display: flex; align-items: center; padding: 20rpx 24rpx; position: relative; }
.check { width: 40rpx; height: 40rpx; border-radius: 50%; border: 2rpx solid #ccc; margin-right: 16rpx; display: flex; align-items: center; justify-content: center; color: transparent; font-size: 24rpx; flex-shrink: 0; }
.check.checked { background: #e64340; border-color: #e64340; color: #fff; }
.item-img { width: 140rpx; height: 140rpx; border-radius: 8rpx; flex-shrink: 0; }
.placeholder { display: flex; align-items: center; justify-content: center; color: #bbb; background: #f5f6f7; font-size: 20rpx; }
.item-info { flex: 1; margin-left: 16rpx; }
.item-name { font-size: 28rpx; }
.item-spec { font-size: 24rpx; color: #999; margin-top: 6rpx; }
.item-bottom { display: flex; justify-content: space-between; align-items: center; margin-top: 12rpx; }
.item-price { color: #e64340; font-size: 30rpx; }
.stepper { display: flex; align-items: center; }
.step-btn { width: 52rpx; height: 52rpx; background: #f5f6f7; text-align: center; line-height: 52rpx; }
.step-num { width: 64rpx; text-align: center; font-size: 26rpx; }
.remove { position: absolute; top: 10rpx; right: 16rpx; color: #bbb; font-size: 24rpx; padding: 8rpx; }
.checkout-bar { position: fixed; bottom: 0; left: 0; right: 0; display: flex; align-items: center; background: #fff; padding: 20rpx 24rpx; padding-bottom: calc(20rpx + env(safe-area-inset-bottom)); border-top: 1px solid #eee; }
.all-label { font-size: 26rpx; margin-left: 8rpx; }
.total { flex: 1; text-align: right; font-size: 26rpx; }
.total-price { color: #e64340; font-size: 32rpx; font-weight: 500; }
.checkout-btn { margin-left: 20rpx; background: #e64340; color: #fff; padding: 18rpx 48rpx; border-radius: 40rpx; font-size: 28rpx; }
</style>

<template>
  <view class="page" v-if="order">
    <view class="status-card">
      <view class="status-text">{{ statusText(order.status) }}</view>
      <view class="status-sub">订单号：{{ order.orderNo }}</view>
    </view>

    <view class="card">
      <view v-for="sub in order.subOrders" :key="sub.id" class="sub-block">
        <view class="merchant">🏬 {{ sub.merchantName }}</view>
        <view v-for="item in sub.items" :key="item.id" class="item-line">
          <view class="item-info">
            <view class="item-name">{{ item.productName }}</view>
            <view class="item-spec">{{ item.spec }}</view>
          </view>
          <view class="item-right">
            <text>¥ {{ item.unitPrice.toFixed(2) }}</text>
            <text class="item-qty">×{{ item.quantity }}</text>
          </view>
        </view>
        <view class="sub-total">小计：¥ {{ sub.totalAmount.toFixed(2) }}</view>
      </view>
    </view>

    <view class="card total-card">
      <text>订单总额</text>
      <text class="amount">¥ {{ order.totalAmount.toFixed(2) }}</text>
    </view>

    <!-- 操作：待付款 → 去支付 / 取消；已发货 → 联系客服 -->
    <view class="action-bar">
      <template v-if="order.status === 1">
        <view class="action-btn plain" @click="cancelOrder">取消订单</view>
        <view class="action-btn primary" @click="pay">去支付</view>
      </template>
      <view v-if="order.status === 3" class="action-btn primary" @click="goChat">联系客服</view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { onLoad } from '@dcloudio/uni-app'
import { orderApi, payApi, type Order } from '../../api'

const order = ref<Order | null>(null)

function statusText(s: number) {
  return { 1: '待付款', 2: '待发货', 3: '待收货', 4: '已完成', 5: '已取消' }[s] || '未知'
}

async function pay() {
  if (!order.value) return
  try {
    const payment = await payApi.create(order.value.id, order.value.totalAmount)
    const result = await payApi.simulatePay(payment.id)
    if (result.status === 2) {
      uni.showToast({ title: '支付成功', icon: 'success' })
      setTimeout(() => load(), 800)
    }
  } catch {
    // 错误已提示
  }
}

function cancelOrder() {
  uni.showModal({
    title: '提示',
    content: '确认取消该订单？',
    success: async (res) => {
      if (res.confirm && order.value) {
        await orderApi.cancel(order.value.id)
        uni.showToast({ title: '已取消', icon: 'none' })
        load()
      }
    },
  })
}

function goChat() {
  const firstSub = order.value?.subOrders?.[0]
  if (firstSub) {
    uni.navigateTo({ url: `/pages/im/chat?merchantId=${firstSub.merchantId}&orderNo=${order.value?.orderNo}` })
  }
}

async function load() {
  order.value = await orderApi.detail(order.value!.id)
}

onLoad(async (query) => {
  const id = query?.id as string
  order.value = await orderApi.detail(id)
})
</script>

<style scoped>
.page { padding-bottom: 140rpx; }
.status-card { background: #e64340; color: #fff; padding: 40rpx 32rpx; }
.status-text { font-size: 40rpx; font-weight: 500; }
.status-sub { font-size: 24rpx; margin-top: 12rpx; opacity: 0.9; }
.card { background: #fff; margin: 16rpx; border-radius: 12rpx; padding: 24rpx; }
.sub-block { margin-bottom: 20rpx; }
.merchant { font-weight: 500; padding-bottom: 12rpx; }
.item-line { display: flex; justify-content: space-between; padding: 10rpx 0; }
.item-name { font-size: 28rpx; }
.item-spec { font-size: 24rpx; color: #999; margin-top: 4rpx; }
.item-right { display: flex; flex-direction: column; align-items: flex-end; font-size: 26rpx; }
.item-qty { color: #999; }
.sub-total { text-align: right; font-size: 26rpx; color: #666; border-top: 1px solid #f5f6f7; padding-top: 12rpx; }
.total-card { display: flex; justify-content: space-between; font-size: 28rpx; }
.amount { color: #e64340; font-size: 34rpx; font-weight: 500; }
.action-bar { position: fixed; bottom: 0; left: 0; right: 0; display: flex; justify-content: flex-end; background: #fff; padding: 16rpx 24rpx; padding-bottom: calc(16rpx + env(safe-area-inset-bottom)); }
.action-btn { padding: 18rpx 44rpx; border-radius: 40rpx; font-size: 28rpx; margin-left: 16rpx; }
.action-btn.plain { border: 1px solid #ddd; color: #666; }
.action-btn.primary { background: #e64340; color: #fff; }
</style>

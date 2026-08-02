<template>
  <view class="page">
    <!-- 状态 Tab -->
    <view class="tabs">
      <view v-for="t in tabs" :key="t.value" class="tab" :class="{ active: activeTab === t.value }"
            @click="switchTab(t.value)">
        {{ t.label }}
      </view>
    </view>

    <view v-if="loading" class="tip">加载中…</view>
    <view v-else-if="list.length === 0" class="empty">暂无订单</view>

    <view v-for="order in list" :key="order.id" class="order-card" @click="goDetail(order.id)">
      <view class="order-head">
        <text class="order-no">{{ order.orderNo }}</text>
        <text class="order-status">{{ statusText(order.status) }}</text>
      </view>
      <view v-for="sub in order.subOrders" :key="sub.id" class="sub-order">
        <view v-for="item in sub.items" :key="item.id" class="item-line">
          <text class="item-name">{{ item.productName }}（{{ item.spec }}）</text>
          <text class="item-qty">×{{ item.quantity }}</text>
        </view>
      </view>
      <view class="order-foot">
        <text>共 {{ totalCount(order) }} 件，合计 </text>
        <text class="amount">¥ {{ order.totalAmount.toFixed(2) }}</text>
      </view>
    </view>

    <view v-if="!loading && !finished" class="load-more" @click="load()">加载更多</view>
  </view>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { onLoad, onPullDownRefresh, onReachBottom } from '@dcloudio/uni-app'
import { orderApi, type Order } from '../../api'

const tabs = [
  { label: '全部', value: '' },
  { label: '待付款', value: '1' },
  { label: '待发货', value: '2' },
  { label: '待收货', value: '3' },
  { label: '已完成', value: '4' },
]

const activeTab = ref('')
const list = ref<Order[]>([])
const page = ref(1)
const loading = ref(false)
const finished = ref(false)

function statusText(s: number) {
  return { 1: '待付款', 2: '待发货', 3: '待收货', 4: '已完成', 5: '已取消' }[s] || '未知'
}

function totalCount(order: Order) {
  return order.subOrders.reduce((sum, sub) => sum + sub.items.reduce((s, i) => s + i.quantity, 0), 0)
}

async function load(reset = false) {
  if (loading.value) return
  loading.value = true
  try {
    const p = reset ? 1 : page.value
    const res = await orderApi.list(p, 10)
    list.value = reset ? res.items : [...list.value, ...res.items]
    page.value = p + 1
    finished.value = list.value.length >= res.totalCount
  } finally {
    loading.value = false
  }
}

function switchTab(v: string) {
  activeTab.value = v
  list.value = []
  page.value = 1
  finished.value = false
  load(true)
}

function goDetail(id: string) {
  uni.navigateTo({ url: `/pages/order/detail?id=${id}` })
}

onLoad(() => load(true))
onPullDownRefresh(async () => {
  await load(true)
  uni.stopPullDownRefresh()
})
onReachBottom(() => load())
</script>

<style scoped>
.tabs { display: flex; background: #fff; position: sticky; top: 0; z-index: 10; }
.tab { flex: 1; text-align: center; padding: 24rpx 0; font-size: 28rpx; color: #666; }
.tab.active { color: #e64340; font-weight: 500; border-bottom: 4rpx solid #e64340; }
.tip { text-align: center; padding: 60rpx; color: #999; }
.empty { text-align: center; padding: 120rpx 0; color: #999; }
.order-card { background: #fff; margin: 16rpx; border-radius: 12rpx; padding: 20rpx 24rpx; }
.order-head { display: flex; justify-content: space-between; border-bottom: 1px solid #f5f6f7; padding-bottom: 14rpx; }
.order-no { font-size: 24rpx; color: #999; }
.order-status { font-size: 26rpx; color: #e64340; }
.item-line { display: flex; justify-content: space-between; padding: 10rpx 0; font-size: 28rpx; }
.item-qty { color: #999; }
.order-foot { text-align: right; font-size: 26rpx; border-top: 1px solid #f5f6f7; padding-top: 14rpx; }
.amount { color: #e64340; font-size: 30rpx; font-weight: 500; }
.load-more { text-align: center; color: #999; padding: 24rpx; }
</style>

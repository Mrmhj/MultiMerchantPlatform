<template>
  <div class="orders">
    <h2 class="page-title">我的订单</h2>
    <el-card v-for="o in orders" :key="o.id" class="order-card" @click="$router.push(`/orders/${o.id}`)">
      <div class="order-head">
        <span class="order-no">{{ o.orderNo }}</span>
        <el-tag :type="statusTag(o.status)">{{ statusText(o.status) }}</el-tag>
      </div>
      <div class="order-body">
        <div class="order-items">
          <div v-for="sub in o.subOrders" :key="sub.id" class="sub-order">
            <div class="sub-head">{{ sub.merchantName }}（{{ sub.items.length }} 件）</div>
            <div v-for="item in sub.items" :key="item.id" class="item-row">
              <span>{{ item.productName }} × {{ item.quantity }}</span>
              <span>¥ {{ item.subtotal.toFixed(2) }}</span>
            </div>
          </div>
        </div>
        <div class="order-total">
          合计：<span class="total">¥ {{ o.totalAmount.toFixed(2) }}</span>
        </div>
      </div>
    </el-card>
    <el-empty v-if="!loading && orders.length === 0" description="暂无订单" />
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { orderApi, type Order } from '../api'

const orders = ref<Order[]>([])
const loading = ref(false)

const statusText = (s: number) => ['', '待付款', '已付款', '已完成', '已取消'][s] || '未知'
const statusTag = (s: number) => (['', 'warning', 'primary', 'success', 'info'][s] as any) || 'info'

onMounted(async () => {
  loading.value = true
  try {
    const res = await orderApi.list(1, 10)
    orders.value = res.items
  } finally {
    loading.value = false
  }
})
</script>

<style scoped>
.page-title { margin-bottom: 16px; }
.order-card { margin-bottom: 16px; cursor: pointer; }
.order-head { display: flex; justify-content: space-between; align-items: center; padding-bottom: 12px; border-bottom: 1px solid #ebeef5; margin-bottom: 12px; }
.order-no { font-weight: 500; }
.sub-order { margin-bottom: 10px; }
.sub-head { color: #909399; font-size: 13px; margin-bottom: 4px; }
.item-row { display: flex; justify-content: space-between; padding: 2px 0; font-size: 14px; }
.order-total { text-align: right; margin-top: 12px; }
.total { color: #e4393c; font-weight: 700; }
</style>

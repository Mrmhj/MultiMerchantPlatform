<template>
  <el-card v-loading="loading" class="detail-card">
    <template #header>
      <div class="head">
        <span>订单详情 {{ order?.orderNo }}</span>
        <el-tag v-if="order" :type="statusTag(order.status)">{{ statusText(order.status) }}</el-tag>
      </div>
    </template>
    <div v-if="order">
      <el-descriptions :column="1" border class="desc">
        <el-descriptions-item label="订单号">{{ order.orderNo }}</el-descriptions-item>
        <el-descriptions-item label="总金额">¥ {{ order.totalAmount.toFixed(2) }}</el-descriptions-item>
        <el-descriptions-item label="下单时间">{{ formatTime(order.createdAt) }}</el-descriptions-item>
        <el-descriptions-item label="备注">{{ order.remark || '无' }}</el-descriptions-item>
      </el-descriptions>

      <div v-for="sub in order.subOrders" :key="sub.id" class="sub-card">
        <div class="sub-head">
          {{ sub.merchantName }}
          <el-tag size="small" :type="statusTag(sub.status)">{{ subStatusText(sub.status) }}</el-tag>
        </div>
        <div v-for="item in sub.items" :key="item.id" class="item-row">
          <span>{{ item.productName }}（{{ item.spec }}）× {{ item.quantity }}</span>
          <span>¥ {{ item.subtotal.toFixed(2) }}</span>
        </div>
      </div>

      <div class="actions">
        <el-button v-if="order.status === 1" type="danger" @click="payNow">立即支付</el-button>
        <el-button v-if="order.status === 1" @click="cancelOrder">取消订单</el-button>
        <el-button @click="$router.push('/orders')">返回列表</el-button>
      </div>
    </div>
  </el-card>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { orderApi, payApi, type Order } from '../api'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const order = ref<Order | null>(null)

const statusText = (s: number) => ['', '待付款', '已付款', '已完成', '已取消'][s] || '未知'
const subStatusText = (s: number) => ['', '待付款', '已付款', '已发货', '已完成', '已取消'][s] || '未知'
const statusTag = (s: number) => (['', 'warning', 'primary', 'success', 'info'][s] as any) || 'info'
const formatTime = (t: string) => new Date(t).toLocaleString('zh-CN')

onMounted(async () => {
  loading.value = true
  try {
    order.value = await orderApi.detail(route.params.id as string)
  } finally {
    loading.value = false
  }
})

async function payNow() {
  if (!order.value) return
  const payment = await payApi.create(order.value.id, order.value.totalAmount)
  await payApi.simulatePay(payment.id)
  ElMessage.success('支付成功')
  order.value = await orderApi.detail(order.value.id)
}

async function cancelOrder() {
  if (!order.value) return
  order.value = await orderApi.cancel(order.value.id)
  ElMessage.success('订单已取消')
}
</script>

<style scoped>
.detail-card { max-width: 760px; margin: 0 auto; }
.head { display: flex; justify-content: space-between; align-items: center; }
.desc { margin-bottom: 20px; }
.sub-card { border: 1px solid #ebeef5; border-radius: 8px; padding: 14px; margin-bottom: 12px; }
.sub-head { display: flex; justify-content: space-between; align-items: center; font-weight: 500; margin-bottom: 8px; }
.item-row { display: flex; justify-content: space-between; padding: 3px 0; font-size: 14px; color: #606266; }
.actions { margin-top: 20px; display: flex; gap: 8px; }
</style>

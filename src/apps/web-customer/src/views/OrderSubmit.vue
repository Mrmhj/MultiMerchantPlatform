<template>
  <el-card v-loading="loading" class="submit-card">
    <template #header>确认订单</template>
    <div v-if="product && sku" class="order-body">
      <el-descriptions :column="1" border>
        <el-descriptions-item label="商品">{{ product.name }}</el-descriptions-item>
        <el-descriptions-item label="规格">{{ sku.spec }}（{{ sku.skuCode }}）</el-descriptions-item>
        <el-descriptions-item label="单价">¥ {{ sku.price.toFixed(2) }}</el-descriptions-item>
        <el-descriptions-item label="数量">{{ quantity }}</el-descriptions-item>
        <el-descriptions-item label="商品小计">¥ {{ subtotal.toFixed(2) }}</el-descriptions-item>
        <el-descriptions-item label="商户">{{ product.merchantId ? '商户商品' : '--' }}</el-descriptions-item>
      </el-descriptions>
      <div class="total-row">
        应付总额：<span class="total">¥ {{ subtotal.toFixed(2) }}</span>
      </div>
      <el-button type="danger" size="large" :loading="submitting" @click="submitOrder">提交订单并支付</el-button>
    </div>
    <el-empty v-else description="订单信息缺失，请返回商品页" />
  </el-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { orderApi, payApi, productApi, type Product, type SkuInfo } from '../api'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const submitting = ref(false)
const product = ref<Product | null>(null)
const quantity = ref(1)

const sku = computed<SkuInfo | undefined>(() =>
  product.value?.skus.find(s => s.id === (route.query.skuId as string)),
)
const subtotal = computed(() => (sku.value ? sku.value.price * quantity.value : 0))

onMounted(async () => {
  loading.value = true
  try {
    product.value = await productApi.detail(route.query.productId as string)
    quantity.value = Number(route.query.quantity) || 1
  } finally {
    loading.value = false
  }
})

async function submitOrder() {
  if (!product.value || !sku.value) return
  submitting.value = true
  try {
    // ① 下单（order-service 自动预占库存）
    const order = await orderApi.create([
      {
        merchantId: product.value.merchantId,
        merchantName: '商户',
        productId: product.value.id,
        productName: product.value.name,
        skuId: sku.value.id,
        skuCode: sku.value.skuCode,
        spec: sku.value.spec,
        unitPrice: sku.value.price,
        quantity: quantity.value,
      },
    ])
    ElMessage.success(`下单成功：${order.orderNo}`)

    // ② 创建支付单并模拟支付（pay-service 回调订单 → 扣减库存）
    const payment = await payApi.create(order.id, order.totalAmount)
    await payApi.simulatePay(payment.id)
    ElMessage.success('支付成功')

    router.push({ name: 'order-detail', params: { id: order.id } })
  } catch {
    // 拦截器已提示（如库存不足）
  } finally {
    submitting.value = false
  }
}
</script>

<style scoped>
.submit-card { max-width: 640px; margin: 0 auto; }
.order-body .el-descriptions { margin-bottom: 20px; }
.total-row { text-align: right; margin-bottom: 16px; font-size: 15px; }
.total { color: #e4393c; font-size: 22px; font-weight: 700; }
</style>

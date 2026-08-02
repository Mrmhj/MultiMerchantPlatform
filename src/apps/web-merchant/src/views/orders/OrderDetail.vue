<template>
  <el-card shadow="never" v-loading="loading">
    <template #header>
      <div class="toolbar">
        <b>子订单详情</b>
        <el-button @click="$router.back()">返回</el-button>
      </div>
    </template>

    <template v-if="order">
      <el-descriptions :column="3" border style="margin-bottom: 16px">
        <el-descriptions-item label="子订单 ID">{{ order.id }}</el-descriptions-item>
        <el-descriptions-item label="主订单 ID">{{ order.orderId }}</el-descriptions-item>
        <el-descriptions-item label="状态">
          <el-tag :type="statusTag(order.status)">{{ statusText(order.status) }}</el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="订单金额">{{ Number(order.totalAmount).toFixed(2) }} 元</el-descriptions-item>
        <el-descriptions-item label="物流单号">{{ order.trackingNo || '-' }}</el-descriptions-item>
        <el-descriptions-item label="物流公司">{{ order.carrierCode || '-' }}</el-descriptions-item>
      </el-descriptions>

      <el-table :data="order.items" border size="small" style="margin-bottom: 16px">
        <el-table-column prop="productName" label="商品" min-width="180" />
        <el-table-column prop="spec" label="规格" width="120" />
        <el-table-column prop="skuCode" label="SKU 编码" width="140" />
        <el-table-column label="单价（元）" width="100" align="right">
          <template #default="{ row }">{{ Number(row.unitPrice).toFixed(2) }}</template>
        </el-table-column>
        <el-table-column prop="quantity" label="数量" width="80" align="center" />
      </el-table>

      <div v-if="order.status === 2" class="actions">
        <el-button type="primary" @click="openShip">发货</el-button>
        <el-button @click="complete">确认完成</el-button>
      </div>
      <el-alert v-else-if="order.status === 3" type="success" :closable="false"
                title="订单已发货，等待买家确认收货" style="max-width: 420px" />
    </template>

    <!-- 发货弹窗 -->
    <el-dialog v-model="shipDialog" title="订单发货" width="460px">
      <el-form :model="shipForm" label-width="80px">
        <el-form-item label="物流公司">
          <el-select v-model="shipForm.carrierCode" placeholder="选择物流公司" style="width: 100%">
            <el-option v-for="c in companies" :key="c.code" :label="c.name" :value="c.code" />
          </el-select>
        </el-form-item>
        <el-form-item label="运单号">
          <el-input v-model="shipForm.trackingNo" placeholder="快递运单号（6-64 位）" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="shipDialog = false">取消</el-button>
        <el-button type="primary" :loading="shipping" @click="ship">确认发货</el-button>
      </template>
    </el-dialog>
  </el-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { orderApi, logisticsApi, type LogisticsCompany, type SubOrder } from '../../api'

const route = useRoute()
const loading = ref(false)
const order = ref<SubOrder | null>(null)
const companies = ref<LogisticsCompany[]>([])
const shipDialog = ref(false)
const shipping = ref(false)
const shipForm = reactive({ carrierCode: '', trackingNo: '' })

const statusMap: Record<number, { text: string; tag: string }> = {
  1: { text: '待付款', tag: 'warning' }, 2: { text: '已付款', tag: 'primary' },
  3: { text: '已发货', tag: 'success' }, 4: { text: '已完成', tag: 'info' }, 5: { text: '已取消', tag: 'danger' },
}
function statusText(s: number) { return statusMap[s]?.text ?? '未知' }
function statusTag(s: number) { return (statusMap[s]?.tag as any) ?? 'info' }

async function load() {
  loading.value = true
  try {
    order.value = await orderApi.merchantDetail(route.params.id as string)
  } finally {
    loading.value = false
  }
}

async function openShip() {
  companies.value = await logisticsApi.companies()
  shipDialog.value = true
}

async function ship() {
  if (!shipForm.carrierCode || shipForm.trackingNo.trim().length < 6) {
    ElMessage.warning('请选择物流公司并填写运单号（≥6 位）')
    return
  }
  shipping.value = true
  try {
    await orderApi.ship(order.value!.id, { carrierCode: shipForm.carrierCode, trackingNo: shipForm.trackingNo.trim() })
    ElMessage.success('发货成功（已自动创建物流运单）')
    shipDialog.value = false
    load()
  } finally {
    shipping.value = false
  }
}

async function complete() {
  await orderApi.complete(order.value!.id)
  ElMessage.success('订单已确认完成')
  load()
}

onMounted(load)
</script>

<style scoped>
.toolbar { display: flex; justify-content: space-between; }
.actions { display: flex; gap: 12px; }
</style>

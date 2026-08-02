<template>
  <el-card shadow="never">
    <template #header>
      <b>库存管理</b>
    </template>

    <el-table :data="list" v-loading="loading" border>
      <el-table-column prop="skuCode" label="SKU 编码" min-width="150" />
      <el-table-column prop="spec" label="规格" width="120" />
      <el-table-column label="总库存" width="100" align="center">
        <template #default="{ row }">{{ row.total }}</template>
      </el-table-column>
      <el-table-column label="预占" width="100" align="center">
        <template #default="{ row }">
          <el-tag type="warning" size="small">{{ row.reserved }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="可用" width="100" align="center">
        <template #default="{ row }">
          <el-tag :type="row.available > 0 ? 'success' : 'danger'" size="small">{{ row.available }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="180" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openIncrease(row)">补货</el-button>
          <el-button link type="info" @click="openTransactions(row)">流水</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination style="margin-top: 16px; justify-content: flex-end" layout="total, prev, pager, next"
                   :total="total" :page-size="query.pageSize" v-model:current-page="query.page" @current-change="load" />

    <!-- 补货弹窗 -->
    <el-dialog v-model="increaseDialog" title="库存补货" width="380px">
      <el-form label-width="80px">
        <el-form-item label="SKU">{{ current?.skuCode }}（{{ current?.spec }}）</el-form-item>
        <el-form-item label="补货数量">
          <el-input-number v-model="increaseQty" :min="1" :precision="0" style="width: 100%" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="increaseDialog = false">取消</el-button>
        <el-button type="primary" :loading="increasing" @click="increase">确认补货</el-button>
      </template>
    </el-dialog>

    <!-- 流水弹窗 -->
    <el-dialog v-model="txDialog" title="库存流水" width="560px">
      <el-table :data="txList" size="small" border>
        <el-table-column label="类型" width="100">
          <template #default="{ row }">{{ txText(row.type) }}</template>
        </el-table-column>
        <el-table-column label="数量" width="90" align="right">
          <template #default="{ row }">{{ row.quantity }}</template>
        </el-table-column>
        <el-table-column prop="referenceId" label="关联单号" min-width="180" show-overflow-tooltip />
        <el-table-column label="时间" width="160">
          <template #default="{ row }">{{ new Date(row.createdAt).toLocaleString('zh-CN') }}</template>
        </el-table-column>
      </el-table>
    </el-dialog>
  </el-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { stockApi, type StockInfo, type StockTransaction } from '../../api'

const loading = ref(false)
const list = ref<StockInfo[]>([])
const total = ref(0)
const query = reactive({ page: 1, pageSize: 20 })

const current = ref<StockInfo | null>(null)
const increaseDialog = ref(false)
const increaseQty = ref(10)
const increasing = ref(false)
const txDialog = ref(false)
const txList = ref<StockTransaction[]>([])

const txMap: Record<number, string> = { 1: '创建', 2: '预占', 3: '扣减', 4: '释放', 5: '补货' }
function txText(t: number) { return txMap[t] ?? `未知(${t})` }

async function load() {
  loading.value = true
  try {
    const res = await stockApi.list({ page: query.page, pageSize: query.pageSize })
    list.value = res.items
    total.value = res.totalCount
  } finally {
    loading.value = false
  }
}

function openIncrease(row: StockInfo) {
  current.value = row
  increaseQty.value = 10
  increaseDialog.value = true
}

async function increase() {
  if (!current.value) return
  increasing.value = true
  try {
    await stockApi.increase(current.value.skuId, increaseQty.value)
    ElMessage.success(`已补货 ${increaseQty.value} 件`)
    increaseDialog.value = false
    load()
  } finally {
    increasing.value = false
  }
}

async function openTransactions(row: StockInfo) {
  current.value = row
  txList.value = await stockApi.transactions(row.skuId)
  txDialog.value = true
}

onMounted(load)
</script>

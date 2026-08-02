<template>
  <div class="settlements">
    <!-- 概览 -->
    <el-row :gutter="16" style="margin-bottom: 16px">
      <el-col :span="6"><el-card shadow="never" class="stat-card">
        <div class="stat-label">待结算单数</div><div class="stat-value">{{ summary?.pendingCount ?? '-' }}</div>
      </el-card></el-col>
      <el-col :span="6"><el-card shadow="never" class="stat-card">
        <div class="stat-label">已结算单数</div><div class="stat-value">{{ summary?.settledCount ?? '-' }}</div>
      </el-card></el-col>
      <el-col :span="6"><el-card shadow="never" class="stat-card">
        <div class="stat-label">已打款单数</div><div class="stat-value">{{ summary?.paidCount ?? '-' }}</div>
      </el-card></el-col>
      <el-col :span="6"><el-card shadow="never" class="stat-card">
        <div class="stat-label">待结算金额（元）</div><div class="stat-value">{{ fmt(summary?.pendingAmount) }}</div>
      </el-card></el-col>
    </el-row>

    <el-card shadow="never">
      <template #header>
        <div class="toolbar">
          <div>
            <el-select v-model="query.status" placeholder="状态" clearable style="width: 140px" @change="load">
              <el-option label="待结算" value="pending" />
              <el-option label="已结算" value="settled" />
              <el-option label="已打款" value="paid" />
            </el-select>
            <el-button type="primary" style="margin-left: 8px" @click="load">查询</el-button>
          </div>
          <el-tag v-if="commission" :type="commission.isDefault ? 'warning' : 'success'" size="default">
            当前佣金比例：{{ commission.rate }}%{{ commission.isDefault ? '（平台默认）' : '' }}
          </el-tag>
        </div>
      </template>

      <el-table :data="list" v-loading="loading" border>
        <el-table-column label="结算周期" min-width="200">
          <template #default="{ row }">{{ fmtTime(row.cycleStart) }} ~ {{ fmtTime(row.cycleEnd) }}</template>
        </el-table-column>
        <el-table-column label="订单金额（元）" width="130" align="right">
          <template #default="{ row }">{{ Number(row.totalOrderAmount).toFixed(2) }}</template>
        </el-table-column>
        <el-table-column label="佣金（元）" width="110" align="right">
          <template #default="{ row }">{{ Number(row.totalCommission).toFixed(2) }}</template>
        </el-table-column>
        <el-table-column label="结算金额（元）" width="130" align="right">
          <template #default="{ row }"><b>{{ Number(row.settlementAmount).toFixed(2) }}</b></template>
        </el-table-column>
        <el-table-column label="状态" width="100" align="center">
          <template #default="{ row }">
            <el-tag :type="row.status === 'Pending' ? 'warning' : row.status === 'Settled' ? 'primary' : 'success'">
              {{ statusText(row.status) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="90" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" @click="openDetail(row)">明细</el-button>
          </template>
        </el-table-column>
      </el-table>

      <el-pagination style="margin-top: 16px; justify-content: flex-end" layout="total, prev, pager, next"
                     :total="total" :page-size="query.pageSize" v-model:current-page="query.page" @current-change="load" />
    </el-card>

    <!-- 结算单详情 -->
    <el-dialog v-model="detailDialog" title="结算单明细" width="640px">
      <template v-if="detail">
        <el-descriptions :column="3" border size="small" style="margin-bottom: 12px">
          <el-descriptions-item label="周期">{{ fmtTime(detail.cycleStart) }}</el-descriptions-item>
          <el-descriptions-item label="订单总额">{{ Number(detail.totalOrderAmount).toFixed(2) }} 元</el-descriptions-item>
          <el-descriptions-item label="佣金">{{ Number(detail.totalCommission).toFixed(2) }} 元</el-descriptions-item>
          <el-descriptions-item label="结算金额"><b>{{ Number(detail.settlementAmount).toFixed(2) }} 元</b></el-descriptions-item>
          <el-descriptions-item label="状态">{{ statusText(detail.status) }}</el-descriptions-item>
          <el-descriptions-item label="打款时间">{{ detail.paidAt ? fmtTime(detail.paidAt) : '-' }}</el-descriptions-item>
        </el-descriptions>
        <el-table :data="detail.items" size="small" border max-height="360">
          <el-table-column prop="orderNo" label="子订单号" min-width="180" show-overflow-tooltip />
          <el-table-column label="商品金额（元）" width="110" align="right">
            <template #default="{ row }">{{ Number(row.productAmount).toFixed(2) }}</template>
          </el-table-column>
          <el-table-column label="佣金（元）" width="100" align="right">
            <template #default="{ row }">{{ Number(row.commissionAmount).toFixed(2) }}</template>
          </el-table-column>
          <el-table-column label="结算金额（元）" width="110" align="right">
            <template #default="{ row }">{{ Number(row.settleAmount).toFixed(2) }}</template>
          </el-table-column>
        </el-table>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { settlementApi, type Settlement, type SettlementSummary } from '../../api'

const loading = ref(false)
const list = ref<Settlement[]>([])
const total = ref(0)
const query = reactive({ page: 1, pageSize: 20, status: '' as string })
const summary = ref<SettlementSummary | null>(null)
const commission = ref<{ rate: number; isDefault: boolean } | null>(null)
const detailDialog = ref(false)
const detail = ref<Settlement | null>(null)

function statusText(s: string) { return s === 'Pending' ? '待结算' : s === 'Settled' ? '已结算' : s === 'Paid' ? '已打款' : s }
function fmtTime(t?: string) { return t ? new Date(t).toLocaleString('zh-CN') : '-' }
function fmt(v?: number) { return v === undefined ? '-' : Number(v).toFixed(2) }

async function load() {
  loading.value = true
  try {
    const res = await settlementApi.list({
      page: query.page, pageSize: query.pageSize, status: query.status || undefined,
    })
    list.value = res.items
    total.value = res.totalCount
  } finally {
    loading.value = false
  }
}

async function openDetail(row: Settlement) {
  detail.value = await settlementApi.detail(row.id)
  detailDialog.value = true
}

onMounted(async () => {
  load()
  try {
    summary.value = await settlementApi.summary()
    commission.value = await settlementApi.commission()
  } catch {
    // 静默
  }
})
</script>

<style scoped>
.stat-card { text-align: center; }
.stat-label { color: #909399; font-size: 13px; }
.stat-value { font-size: 20px; font-weight: 500; margin-top: 6px; }
.toolbar { display: flex; justify-content: space-between; align-items: center; }
</style>

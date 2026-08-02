<template>
  <el-card shadow="never">
    <template #header>
      <div class="toolbar">
        <div>
          <el-select v-model="query.status" placeholder="运单状态" clearable style="width: 150px" @change="load">
            <el-option label="待揽收" value="created" />
            <el-option label="运输中" value="intransit" />
            <el-option label="派送中" value="outfordelivery" />
            <el-option label="已签收" value="signed" />
            <el-option label="异常" value="exception" />
          </el-select>
          <el-button type="primary" style="margin-left: 8px" @click="load">查询</el-button>
        </div>
      </div>
    </template>

    <el-table :data="list" v-loading="loading" border>
      <el-table-column prop="orderNo" label="订单号" width="170" show-overflow-tooltip />
      <el-table-column prop="carrierName" label="物流公司" width="110" />
      <el-table-column prop="trackingNo" label="运单号" width="170" show-overflow-tooltip />
      <el-table-column label="状态" width="100" align="center">
        <template #default="{ row }">
          <el-tag :type="statusTag(row.status)">{{ statusText(row.status) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="签收时间" width="170">
        <template #default="{ row }">{{ row.signedAt ? new Date(row.signedAt).toLocaleString('zh-CN') : '-' }}</template>
      </el-table-column>
      <el-table-column prop="createdAt" label="创建时间" width="170">
        <template #default="{ row }">{{ new Date(row.createdAt).toLocaleString('zh-CN') }}</template>
      </el-table-column>
      <el-table-column label="操作" width="90" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openDetail(row)">轨迹</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination style="margin-top: 16px; justify-content: flex-end" layout="total, prev, pager, next"
                   :total="total" :page-size="query.pageSize" v-model:current-page="query.page" @current-change="load" />

    <!-- 运单详情（轨迹） -->
    <el-dialog v-model="detailDialog" title="物流轨迹" width="520px">
      <template v-if="detail">
        <el-descriptions :column="2" border size="small" style="margin-bottom: 12px">
          <el-descriptions-item label="运单号">{{ detail.trackingNo }}</el-descriptions-item>
          <el-descriptions-item label="物流公司">{{ detail.carrierName }}</el-descriptions-item>
          <el-descriptions-item label="订单号" :span="2">{{ detail.orderNo }}</el-descriptions-item>
        </el-descriptions>
        <el-timeline>
          <el-timeline-item v-for="(t, i) in detail.tracks" :key="i" :timestamp="new Date(t.trackedAt).toLocaleString('zh-CN')"
                            :type="t.status === 4 ? 'success' : t.status === 5 ? 'danger' : 'primary'">
            {{ trackText(t.status) }} - {{ t.description }}{{ t.location ? `（${t.location}）` : '' }}
          </el-timeline-item>
        </el-timeline>
      </template>
    </el-dialog>
  </el-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { logisticsApi, type Shipment } from '../../api'

const loading = ref(false)
const list = ref<Shipment[]>([])
const total = ref(0)
const query = reactive({ page: 1, pageSize: 20, status: '' as string })

const detailDialog = ref(false)
const detail = ref<Shipment | null>(null)

const statusMap: Record<number, { text: string; tag: string }> = {
  1: { text: '待揽收', tag: 'info' }, 2: { text: '运输中', tag: 'primary' },
  3: { text: '派送中', tag: 'warning' }, 4: { text: '已签收', tag: 'success' }, 5: { text: '异常', tag: 'danger' },
}
function statusText(s: number) { return statusMap[s]?.text ?? '未知' }
function statusTag(s: number) { return (statusMap[s]?.tag as any) ?? 'info' }
const trackMap: Record<number, string> = { 1: '已创建', 2: '运输中', 3: '派送中', 4: '已签收', 5: '异常' }
function trackText(s: number) { return trackMap[s] ?? '未知' }

async function load() {
  loading.value = true
  try {
    const res = await logisticsApi.shipments({
      page: query.page, pageSize: query.pageSize, status: query.status || undefined,
    })
    list.value = res.items
    total.value = res.totalCount
  } finally {
    loading.value = false
  }
}

async function openDetail(row: Shipment) {
  detail.value = await logisticsApi.detail(row.id)
  detailDialog.value = true
}

onMounted(load)
</script>

<style scoped>
.toolbar { display: flex; justify-content: space-between; }
</style>

<template>
  <el-card shadow="never">
    <template #header>
      <div class="toolbar">
        <div>
          <el-select v-model="query.status" placeholder="订单状态" clearable style="width: 140px" @change="load">
            <el-option label="待付款" value="1" />
            <el-option label="已付款" value="2" />
            <el-option label="已发货" value="3" />
            <el-option label="已完成" value="4" />
            <el-option label="已取消" value="5" />
          </el-select>
          <el-button type="primary" style="margin-left: 8px" @click="load">查询</el-button>
        </div>
      </div>
    </template>

    <el-table :data="list" v-loading="loading" border>
      <el-table-column prop="id" label="子订单号" width="150" show-overflow-tooltip />
      <el-table-column label="商品" min-width="220">
        <template #default="{ row }">
          <div v-for="item in row.items" :key="item.id" class="item-line">
            {{ item.productName }}（{{ item.spec }}）× {{ item.quantity }}
          </div>
        </template>
      </el-table-column>
      <el-table-column label="金额（元）" width="110" align="right">
        <template #default="{ row }">{{ Number(row.totalAmount).toFixed(2) }}</template>
      </el-table-column>
      <el-table-column label="状态" width="90" align="center">
        <template #default="{ row }">
          <el-tag :type="statusTag(row.status)">{{ statusText(row.status) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="物流" width="150" show-overflow-tooltip>
        <template #default="{ row }">{{ row.trackingNo || '-' }}</template>
      </el-table-column>
      <el-table-column label="操作" width="110" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="$router.push(`/orders/${row.id}`)">详情</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination style="margin-top: 16px; justify-content: flex-end" layout="total, prev, pager, next"
                   :total="total" :page-size="query.pageSize" v-model:current-page="query.page" @current-change="load" />
  </el-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { orderApi, type SubOrder } from '../../api'

const loading = ref(false)
const list = ref<SubOrder[]>([])
const total = ref(0)
const query = reactive({ page: 1, pageSize: 20, status: '' as string })

const statusMap: Record<number, { text: string; tag: string }> = {
  1: { text: '待付款', tag: 'warning' },
  2: { text: '已付款', tag: 'primary' },
  3: { text: '已发货', tag: 'success' },
  4: { text: '已完成', tag: 'info' },
  5: { text: '已取消', tag: 'danger' },
}
function statusText(s: number) { return statusMap[s]?.text ?? '未知' }
function statusTag(s: number) { return (statusMap[s]?.tag as any) ?? 'info' }

async function load() {
  loading.value = true
  try {
    const res = await orderApi.merchantList({
      page: query.page, pageSize: query.pageSize, status: query.status || undefined,
    })
    list.value = res.items
    total.value = res.totalCount
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<style scoped>
.toolbar { display: flex; justify-content: space-between; }
.item-line { line-height: 1.8; }
</style>

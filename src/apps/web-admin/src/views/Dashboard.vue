<template>
  <div class="dashboard">
    <!-- 顶部操作栏 -->
    <el-card class="toolbar" shadow="never">
      <div class="toolbar-inner">
        <div>
          <el-button type="primary" :loading="syncing" @click="onSync">
            <el-icon style="margin-right: 4px"><Refresh /></el-icon>同步数据
          </el-button>
          <span v-if="overview" class="sync-time">
            上次同步：{{ new Date(overview.syncedAt).toLocaleString('zh-CN') }}
          </span>
        </div>
        <div>
          <el-radio-group v-model="days" size="default" @change="loadTrend">
            <el-radio-button :value="7">近 7 天</el-radio-button>
            <el-radio-button :value="30">近 30 天</el-radio-button>
            <el-radio-button :value="90">近 90 天</el-radio-button>
          </el-radio-group>
        </div>
      </div>
    </el-card>

    <!-- 核心指标卡 -->
    <el-row :gutter="16" class="cards">
      <el-col :span="4"><el-card shadow="never" class="metric"><div class="metric-label">累计 GMV</div><div class="metric-value">{{ fmtMoney(overview?.totalGmv ?? 0) }}</div></el-card></el-col>
      <el-col :span="4"><el-card shadow="never" class="metric"><div class="metric-label">订单总数</div><div class="metric-value">{{ overview?.totalOrders ?? 0 }}</div></el-card></el-col>
      <el-col :span="4"><el-card shadow="never" class="metric"><div class="metric-label">已完成订单</div><div class="metric-value">{{ overview?.completedOrders ?? 0 }}</div></el-card></el-col>
      <el-col :span="4"><el-card shadow="never" class="metric"><div class="metric-label">商户数</div><div class="metric-value">{{ overview?.merchantCount ?? 0 }}</div></el-card></el-col>
      <el-col :span="4"><el-card shadow="never" class="metric"><div class="metric-label">商品数</div><div class="metric-value">{{ overview?.productCount ?? 0 }}</div></el-card></el-col>
      <el-col :span="4"><el-card shadow="never" class="metric"><div class="metric-label">用户数</div><div class="metric-value">{{ overview?.userCount ?? 0 }}</div></el-card></el-col>
    </el-row>

    <!-- 销售趋势 -->
    <el-card shadow="never" class="panel">
      <template #header><span class="panel-title">销售趋势（GMV / 订单数）</span></template>
      <div ref="trendRef" class="chart-lg"></div>
    </el-card>

    <!-- 排行 + 状态分布 -->
    <el-row :gutter="16">
      <el-col :span="8">
        <el-card shadow="never" class="panel">
          <template #header><span class="panel-title">商户销售排行 TOP10</span></template>
          <div ref="merchantRef" class="chart-md"></div>
        </el-card>
      </el-col>
      <el-col :span="8">
        <el-card shadow="never" class="panel">
          <template #header><span class="panel-title">商品销售排行 TOP10</span></template>
          <div ref="productRef" class="chart-md"></div>
        </el-card>
      </el-col>
      <el-col :span="8">
        <el-card shadow="never" class="panel">
          <template #header><span class="panel-title">订单状态分布</span></template>
          <div ref="statusRef" class="chart-md"></div>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { Refresh } from '@element-plus/icons-vue'
import * as echarts from 'echarts'
import { biApi, type BiOverview } from '../api'

const overview = ref<BiOverview | null>(null)
const syncing = ref(false)
const days = ref(30)

const trendRef = ref<HTMLElement>()
const merchantRef = ref<HTMLElement>()
const productRef = ref<HTMLElement>()
const statusRef = ref<HTMLElement>()

let trendChart: echarts.ECharts | null = null
let merchantChart: echarts.ECharts | null = null
let productChart: echarts.ECharts | null = null
let statusChart: echarts.ECharts | null = null

const ORDER_STATUS_NAMES: Record<number, string> = {
  1: '待付款', 2: '已付款', 3: '已完成', 4: '已取消',
}

function fmtMoney(v: number): string {
  return '¥' + (v ?? 0).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function disposeCharts() {
  trendChart?.dispose(); merchantChart?.dispose(); productChart?.dispose(); statusChart?.dispose()
  trendChart = merchantChart = productChart = statusChart = null
}

function renderCharts() {
  disposeCharts()
  if (!trendRef.value || !merchantRef.value || !productRef.value || !statusRef.value) return
  trendChart = echarts.init(trendRef.value)
  merchantChart = echarts.init(merchantRef.value)
  productChart = echarts.init(productRef.value)
  statusChart = echarts.init(statusRef.value)
}

async function loadAll() {
  renderCharts()
  await Promise.allSettled([loadOverview(), loadTrend(), loadMerchantRank(), loadProductRank(), loadOrderStatus()])
}

async function loadOverview() {
  overview.value = await biApi.overview()
}

async function loadTrend() {
  const points = await biApi.salesTrend(days.value)
  trendChart?.setOption({
    tooltip: { trigger: 'axis' },
    legend: { data: ['GMV', '订单数'] },
    grid: { left: 60, right: 60, top: 40, bottom: 30 },
    xAxis: { type: 'category', data: points.map((p) => p.date) },
    yAxis: [
      { type: 'value', name: 'GMV（元）' },
      { type: 'value', name: '订单数' },
    ],
    series: [
      { name: 'GMV', type: 'line', smooth: true, data: points.map((p) => p.gmv), areaStyle: { opacity: 0.15 } },
      { name: '订单数', type: 'line', smooth: true, yAxisIndex: 1, data: points.map((p) => p.orderCount) },
    ],
  })
}

async function loadMerchantRank() {
  const items = await biApi.merchantRank(10)
  merchantChart?.setOption({
    tooltip: { trigger: 'axis' },
    grid: { left: 110, right: 40, top: 20, bottom: 30 },
    xAxis: { type: 'value' },
    yAxis: { type: 'category', data: items.map((i) => i.merchantName).reverse() },
    series: [{
      type: 'bar',
      data: items.map((i) => i.gmv).reverse(),
      itemStyle: { color: '#409eff' },
      label: { show: true, position: 'right', formatter: '{c}' },
    }],
  })
}

async function loadProductRank() {
  const items = await biApi.productRank(10)
  productChart?.setOption({
    tooltip: { trigger: 'axis' },
    grid: { left: 110, right: 40, top: 20, bottom: 30 },
    xAxis: { type: 'value' },
    yAxis: { type: 'category', data: items.map((i) => i.productName).reverse() },
    series: [{
      type: 'bar',
      data: items.map((i) => i.amount).reverse(),
      itemStyle: { color: '#67c23a' },
      label: { show: true, position: 'right', formatter: '{c}' },
    }],
  })
}

async function loadOrderStatus() {
  const items = await biApi.orderStatus()
  statusChart?.setOption({
    tooltip: { trigger: 'item', formatter: '{b}: {c}（{d}%）' },
    legend: { bottom: 0 },
    series: [{
      type: 'pie',
      radius: ['40%', '65%'],
      data: items.map((i) => ({ name: ORDER_STATUS_NAMES[i.status] || `状态${i.status}`, value: i.count })),
      label: { formatter: '{b}: {c}' },
    }],
  })
}

async function onSync() {
  syncing.value = true
  try {
    const res = await biApi.sync()
    if (res.success) {
      ElMessage.success(`同步完成：GMV ¥${res.totalGmv}，订单 ${res.totalOrders} 笔`)
      await loadAll()
    } else {
      ElMessage.error(res.error || '同步失败')
    }
  } catch {
    // 错误已由拦截器提示
  } finally {
    syncing.value = false
  }
}

function onResize() {
  trendChart?.resize(); merchantChart?.resize(); productChart?.resize(); statusChart?.resize()
}

onMounted(() => {
  loadAll()
  window.addEventListener('resize', onResize)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', onResize)
  disposeCharts()
})
</script>

<style scoped>
.toolbar-inner { display: flex; align-items: center; justify-content: space-between; }
.sync-time { margin-left: 12px; font-size: 12px; color: #909399; }
.cards { margin-top: 16px; }
.metric { text-align: center; }
.metric-label { font-size: 13px; color: #909399; }
.metric-value { font-size: 22px; font-weight: 600; margin-top: 6px; color: #303133; }
.panel { margin-top: 16px; }
.panel-title { font-weight: 500; }
.chart-lg { height: 320px; }
.chart-md { height: 320px; }
</style>

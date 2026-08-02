<template>
  <div class="dashboard">
    <!-- 入驻未通过 → 引导入驻 -->
    <el-card v-if="!auth.isApproved" style="margin-bottom: 16px">
      <el-result v-if="auth.merchantStatus === 1" icon="info" title="入驻审核中"
                 sub-title="平台正在审核您的入驻申请，通过后可开始管理店铺" />
      <el-result v-else icon="warning" :title="auth.merchantStatus === 3 ? '入驻申请被驳回' : '尚未提交入驻申请'"
                 :sub-title="auth.merchant?.rejectReason || '提交入驻申请后即可开通商户功能'">
        <template #extra>
          <el-button type="primary" @click="$router.push('/apply')">去入驻</el-button>
        </template>
      </el-result>
    </el-card>

    <template v-else>
      <!-- 结算概览 -->
      <el-row :gutter="16">
        <el-col :span="6"><el-card shadow="never" class="stat-card">
          <div class="stat-label">待结算单数</div>
          <div class="stat-value">{{ summary?.pendingCount ?? '-' }}</div>
        </el-card></el-col>
        <el-col :span="6"><el-card shadow="never" class="stat-card">
          <div class="stat-label">待结算金额（元）</div>
          <div class="stat-value">{{ fmt(summary?.pendingAmount) }}</div>
        </el-card></el-col>
        <el-col :span="6"><el-card shadow="never" class="stat-card">
          <div class="stat-label">累计结算金额（元）</div>
          <div class="stat-value">{{ fmt(summary?.settledAmount) }}</div>
        </el-card></el-col>
        <el-col :span="6"><el-card shadow="never" class="stat-card">
          <div class="stat-label">我的佣金比例</div>
          <div class="stat-value">{{ commission ? `${commission.rate}%` : '-' }}</div>
        </el-card></el-col>
      </el-row>

      <!-- 商户信息 -->
      <el-card shadow="never" style="margin-top: 16px">
        <template #header><b>商户信息</b></template>
        <el-descriptions :column="3" border>
          <el-descriptions-item label="商户名称">{{ auth.merchant?.name }}</el-descriptions-item>
          <el-descriptions-item label="营业执照号">{{ auth.merchant?.licenseNo }}</el-descriptions-item>
          <el-descriptions-item label="入驻时间">{{ auth.merchant?.approvedAt || '-' }}</el-descriptions-item>
          <el-descriptions-item label="联系人">{{ auth.merchant?.contactName }}</el-descriptions-item>
          <el-descriptions-item label="联系电话">{{ auth.merchant?.contactPhone }}</el-descriptions-item>
          <el-descriptions-item label="联系邮箱">{{ auth.merchant?.contactEmail || '-' }}</el-descriptions-item>
          <el-descriptions-item label="商户简介" :span="3">{{ auth.merchant?.description || '-' }}</el-descriptions-item>
        </el-descriptions>
      </el-card>
    </template>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { settlementApi } from '../api'
import { useAuthStore } from '../stores/auth'
import type { SettlementSummary } from '../api'

const auth = useAuthStore()
const summary = ref<SettlementSummary | null>(null)
const commission = ref<{ rate: number; isDefault: boolean } | null>(null)

function fmt(v?: number) {
  return v === undefined ? '-' : Number(v).toFixed(2)
}

onMounted(async () => {
  if (!auth.merchant) {
    await auth.fetchMerchant()
  }
  if (auth.isApproved) {
    try {
      summary.value = await settlementApi.summary()
      commission.value = await settlementApi.commission()
    } catch {
      // 网关未启动时静默
    }
  }
})
</script>

<style scoped>
.stat-card { text-align: center; }
.stat-label { color: #909399; font-size: 13px; }
.stat-value { font-size: 22px; font-weight: 500; margin-top: 8px; color: #303133; }
</style>

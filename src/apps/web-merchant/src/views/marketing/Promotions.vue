<template>
  <el-card shadow="never">
    <template #header>
      <b>营销中心</b>
    </template>

    <el-tabs v-model="tab">
      <!-- 满减活动 -->
      <el-tab-pane label="满减活动" name="activities">
        <div class="toolbar">
          <el-select v-model="actQuery.status" placeholder="状态" clearable style="width: 140px" @change="loadActivities">
            <el-option label="草稿" value="draft" />
            <el-option label="进行中" value="active" />
            <el-option label="已结束" value="ended" />
          </el-select>
          <el-button type="primary" @click="openActivity()">新增活动</el-button>
        </div>
        <el-table :data="actList" v-loading="actLoading" border style="margin-top: 12px">
          <el-table-column prop="name" label="活动名称" min-width="180" />
          <el-table-column label="满（元）" width="90" align="right">
            <template #default="{ row }">{{ Number(row.thresholdAmount).toFixed(2) }}</template>
          </el-table-column>
          <el-table-column label="减（元）" width="90" align="right">
            <template #default="{ row }">{{ Number(row.discountAmount).toFixed(2) }}</template>
          </el-table-column>
          <el-table-column label="有效期" min-width="220">
            <template #default="{ row }">{{ fmtTime(row.startTime) }} ~ {{ fmtTime(row.endTime) }}</template>
          </el-table-column>
          <el-table-column label="状态" width="90" align="center">
            <template #default="{ row }">
              <el-tag :type="row.status === 'Active' ? 'success' : row.status === 'Draft' ? 'info' : 'default'">
                {{ actStatusText(row.status) }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="110">
            <template #default="{ row }">
              <el-button v-if="row.status === 'Draft'" link type="success" @click="toggleActivity(row, true)">启用</el-button>
              <el-button v-else-if="row.status === 'Active'" link type="warning" @click="toggleActivity(row, false)">停用</el-button>
              <span v-else>-</span>
            </template>
          </el-table-column>
        </el-table>
        <el-pagination style="margin-top: 12px; justify-content: flex-end" layout="total, prev, pager, next"
                       :total="actTotal" :page-size="20" v-model:current-page="actQuery.page" @current-change="loadActivities" />
      </el-tab-pane>

      <!-- 优惠券 -->
      <el-tab-pane label="优惠券" name="coupons">
        <div class="toolbar">
          <el-button type="primary" @click="openCoupon()">新增优惠券</el-button>
        </div>
        <el-table :data="couponList" v-loading="couponLoading" border style="margin-top: 12px">
          <el-table-column prop="name" label="券名称" min-width="160" />
          <el-table-column label="满（元）" width="90" align="right">
            <template #default="{ row }">{{ Number(row.thresholdAmount).toFixed(2) }}</template>
          </el-table-column>
          <el-table-column label="减（元）" width="90" align="right">
            <template #default="{ row }">{{ Number(row.discountAmount).toFixed(2) }}</template>
          </el-table-column>
          <el-table-column label="总量" width="80" align="center">
            <template #default="{ row }">{{ row.totalQuantity === 0 ? '不限' : row.totalQuantity }}</template>
          </el-table-column>
          <el-table-column label="限领" width="80" align="center">
            <template #default="{ row }">{{ row.limitPerUser }} 张/人</template>
          </el-table-column>
          <el-table-column label="已领" width="80" align="center">
            <template #default="{ row }">{{ row.claimedCount ?? 0 }}</template>
          </el-table-column>
          <el-table-column label="有效期" min-width="210">
            <template #default="{ row }">{{ fmtTime(row.validFrom) }} ~ {{ fmtTime(row.validUntil) }}</template>
          </el-table-column>
          <el-table-column label="操作" width="100">
            <template #default="{ row }">
              <el-button v-if="row.status !== 'Ended'" link :type="row.status === 'Active' ? 'warning' : 'success'"
                         @click="toggleCoupon(row)">
                {{ row.status === 'Active' ? '停用' : '启用' }}
              </el-button>
              <span v-else>-</span>
            </template>
          </el-table-column>
        </el-table>
        <el-pagination style="margin-top: 12px; justify-content: flex-end" layout="total, prev, pager, next"
                       :total="couponTotal" :page-size="20" v-model:current-page="couponQuery.page" @current-change="loadCoupons" />
      </el-tab-pane>
    </el-tabs>

    <!-- 新增满减活动 -->
    <el-dialog v-model="actDialog" title="新增满减活动" width="480px">
      <el-form :model="actForm" label-width="90px">
        <el-form-item label="活动名称"><el-input v-model="actForm.name" placeholder="如：全场满 200 减 30" /></el-form-item>
        <el-form-item label="满（元）"><el-input-number v-model="actForm.thresholdAmount" :min="0.01" :precision="2" /></el-form-item>
        <el-form-item label="减（元）"><el-input-number v-model="actForm.discountAmount" :min="0.01" :precision="2" /></el-form-item>
        <el-form-item label="开始时间">
          <el-date-picker v-model="actForm.startTime" type="datetime" value-format="YYYY-MM-DDTHH:mm:ssZ" />
        </el-form-item>
        <el-form-item label="结束时间">
          <el-date-picker v-model="actForm.endTime" type="datetime" value-format="YYYY-MM-DDTHH:mm:ssZ" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="actDialog = false">取消</el-button>
        <el-button type="primary" :loading="actSaving" @click="saveActivity">创建</el-button>
      </template>
    </el-dialog>

    <!-- 新增优惠券 -->
    <el-dialog v-model="couponDialog" title="新增优惠券" width="480px">
      <el-form :model="couponForm" label-width="90px">
        <el-form-item label="券名称"><el-input v-model="couponForm.name" placeholder="如：满 100 减 20" /></el-form-item>
        <el-form-item label="满（元）"><el-input-number v-model="couponForm.thresholdAmount" :min="0.01" :precision="2" /></el-form-item>
        <el-form-item label="减（元）"><el-input-number v-model="couponForm.discountAmount" :min="0.01" :precision="2" /></el-form-item>
        <el-form-item label="发放总量"><el-input-number v-model="couponForm.totalQuantity" :min="0" :precision="0" /><span class="hint">0 = 不限量</span></el-form-item>
        <el-form-item label="限领张数"><el-input-number v-model="couponForm.limitPerUser" :min="1" :precision="0" /></el-form-item>
        <el-form-item label="生效时间">
          <el-date-picker v-model="couponForm.validFrom" type="datetime" value-format="YYYY-MM-DDTHH:mm:ssZ" />
        </el-form-item>
        <el-form-item label="失效时间">
          <el-date-picker v-model="couponForm.validUntil" type="datetime" value-format="YYYY-MM-DDTHH:mm:ssZ" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="couponDialog = false">取消</el-button>
        <el-button type="primary" :loading="couponSaving" @click="saveCoupon">创建</el-button>
      </template>
    </el-dialog>
  </el-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { promotionApi, type Coupon, type PromotionActivity } from '../../api'

const tab = ref('activities')

// ---------- 满减活动 ----------
const actLoading = ref(false)
const actList = ref<PromotionActivity[]>([])
const actTotal = ref(0)
const actQuery = reactive({ page: 1, pageSize: 20, status: '' as string })
const actDialog = ref(false)
const actSaving = ref(false)
const actForm = reactive({ name: '', thresholdAmount: 100, discountAmount: 20, startTime: '', endTime: '' })

function actStatusText(s: string) { return s === 'Draft' ? '草稿' : s === 'Active' ? '进行中' : '已结束' }
function fmtTime(t?: string) { return t ? new Date(t).toLocaleString('zh-CN') : '-' }

async function loadActivities() {
  actLoading.value = true
  try {
    const res = await promotionApi.activities.list({
      page: actQuery.page, pageSize: actQuery.pageSize, status: actQuery.status || undefined,
    })
    actList.value = res.items
    actTotal.value = res.totalCount
  } finally {
    actLoading.value = false
  }
}

function openActivity() {
  Object.assign(actForm, { name: '', thresholdAmount: 100, discountAmount: 20, startTime: '', endTime: '' })
  actDialog.value = true
}

async function saveActivity() {
  if (!actForm.name.trim() || !actForm.startTime || !actForm.endTime) {
    ElMessage.warning('请填写完整活动信息（名称 + 起止时间）')
    return
  }
  actSaving.value = true
  try {
    await promotionApi.activities.create({ ...actForm })
    ElMessage.success('活动已创建（草稿，启用后生效）')
    actDialog.value = false
    loadActivities()
  } finally {
    actSaving.value = false
  }
}

async function toggleActivity(row: PromotionActivity, active: boolean) {
  await ElMessageBox.confirm(`确认${active ? '启用' : '停用'}活动「${row.name}」？`, '提示', { type: 'warning' })
  await promotionApi.activities.updateStatus(row.id, active)
  ElMessage.success('操作成功')
  loadActivities()
}

// ---------- 优惠券 ----------
const couponLoading = ref(false)
const couponList = ref<Coupon[]>([])
const couponTotal = ref(0)
const couponQuery = reactive({ page: 1, pageSize: 20 })
const couponDialog = ref(false)
const couponSaving = ref(false)
const couponForm = reactive({
  name: '', thresholdAmount: 100, discountAmount: 20,
  totalQuantity: 0, limitPerUser: 1, validFrom: '', validUntil: '',
})

async function loadCoupons() {
  couponLoading.value = true
  try {
    const res = await promotionApi.coupons.list({ page: couponQuery.page, pageSize: couponQuery.pageSize })
    couponList.value = res.items
    couponTotal.value = res.totalCount
  } finally {
    couponLoading.value = false
  }
}

function openCoupon() {
  Object.assign(couponForm, {
    name: '', thresholdAmount: 100, discountAmount: 20,
    totalQuantity: 0, limitPerUser: 1, validFrom: '', validUntil: '',
  })
  couponDialog.value = true
}

async function saveCoupon() {
  if (!couponForm.name.trim() || !couponForm.validFrom || !couponForm.validUntil) {
    ElMessage.warning('请填写完整优惠券信息（名称 + 有效期）')
    return
  }
  couponSaving.value = true
  try {
    await promotionApi.coupons.create({ ...couponForm })
    ElMessage.success('优惠券已创建')
    couponDialog.value = false
    loadCoupons()
  } finally {
    couponSaving.value = false
  }
}

async function toggleCoupon(row: Coupon) {
  const active = row.status !== 'Active'
  await ElMessageBox.confirm(`确认${active ? '启用' : '停用'}优惠券「${row.name}」？`, '提示', { type: 'warning' })
  await promotionApi.coupons.updateStatus(row.id, active)
  ElMessage.success('操作成功')
  loadCoupons()
}

onMounted(() => {
  loadActivities()
  loadCoupons()
})
</script>

<style scoped>
.toolbar { display: flex; justify-content: space-between; }
.hint { font-size: 12px; color: #909399; margin-left: 8px; }
</style>

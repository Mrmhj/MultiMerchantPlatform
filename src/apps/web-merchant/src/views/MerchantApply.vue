<template>
  <div class="apply-page">
    <el-card v-if="!auth.merchant || auth.merchantStatus === 1" class="apply-card">
      <template #header>
        <div class="card-title">商户入驻申请</div>
      </template>
      <el-alert v-if="auth.merchantStatus === 1" type="info" :closable="false"
                title="您的入驻申请正在审核中，请耐心等待平台审核。" style="margin-bottom: 16px" />
      <el-form :model="form" label-width="90px" :rules="rules" ref="formRef">
        <el-form-item label="商户名称" prop="name">
          <el-input v-model="form.name" placeholder="店铺/公司名称（2-100 字）" />
        </el-form-item>
        <el-form-item label="营业执照号" prop="licenseNo">
          <el-input v-model="form.licenseNo" placeholder="营业执照统一社会信用代码（6-50 位）" />
        </el-form-item>
        <el-form-item label="联系人" prop="contactName">
          <el-input v-model="form.contactName" placeholder="联系人姓名" />
        </el-form-item>
        <el-form-item label="联系电话" prop="contactPhone">
          <el-input v-model="form.contactPhone" placeholder="联系电话" />
        </el-form-item>
        <el-form-item label="联系邮箱">
          <el-input v-model="form.contactEmail" placeholder="选填" />
        </el-form-item>
        <el-form-item label="商户简介">
          <el-input v-model="form.description" type="textarea" :rows="3" placeholder="经营类目、店铺介绍（选填）" />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" :loading="loading" @click="onSubmit">提交申请</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card v-else-if="auth.merchantStatus === 2" class="apply-card">
      <el-result icon="success" title="入驻审核已通过" :sub-title="`商户：${auth.merchant?.name}，可开始管理商品与订单`">
        <template #extra>
          <el-button type="primary" @click="$router.push('/products')">去管理商品</el-button>
        </template>
      </el-result>
    </el-card>

    <el-card v-else-if="auth.merchantStatus === 3" class="apply-card">
      <el-result icon="error" title="入驻申请被驳回" :sub-title="auth.merchant?.rejectReason || '请修改资料后重新提交'">
        <template #extra>
          <el-button type="primary" @click="reapply">重新提交申请</el-button>
        </template>
      </el-result>
    </el-card>

    <el-card v-else class="apply-card">
      <el-result icon="warning" title="商户已被禁用" sub-title="如有疑问请联系平台运营" />
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, onMounted } from 'vue'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { merchantApi } from '../api'
import { useAuthStore } from '../stores/auth'

const auth = useAuthStore()
const formRef = ref<FormInstance>()
const loading = ref(false)
const form = reactive({
  name: '', licenseNo: '', contactName: '', contactPhone: '', contactEmail: '', description: '',
})

const rules: FormRules = {
  name: [{ required: true, min: 2, max: 100, message: '请输入商户名称（2-100 字）', trigger: 'blur' }],
  licenseNo: [{ required: true, min: 6, max: 50, message: '请输入营业执照号（6-50 位）', trigger: 'blur' }],
  contactName: [{ required: true, message: '请输入联系人姓名', trigger: 'blur' }],
  contactPhone: [{ required: true, message: '请输入联系电话', trigger: 'blur' }],
}

onMounted(async () => {
  if (!auth.merchant) {
    await auth.fetchMerchant()
  }
  if (auth.merchant) {
    form.name = auth.merchant.name
    form.licenseNo = auth.merchant.licenseNo
    form.contactName = auth.merchant.contactName
    form.contactPhone = auth.merchant.contactPhone
    form.contactEmail = auth.merchant.contactEmail || ''
    form.description = auth.merchant.description || ''
  }
})

async function onSubmit() {
  await formRef.value?.validate()
  loading.value = true
  try {
    await merchantApi.apply({
      name: form.name, licenseNo: form.licenseNo, contactName: form.contactName,
      contactPhone: form.contactPhone, contactEmail: form.contactEmail || undefined,
      description: form.description || undefined,
    })
    ElMessage.success('入驻申请已提交，等待平台审核')
    await auth.fetchMerchant()
  } finally {
    loading.value = false
  }
}

function reapply() {
  auth.merchant = null
}
</script>

<style scoped>
.apply-page { display: flex; justify-content: center; padding-top: 30px; }
.apply-card { width: 560px; }
.card-title { font-size: 16px; font-weight: 500; text-align: center; }
</style>

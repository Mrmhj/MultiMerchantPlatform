<template>
  <el-card shadow="never">
    <template #header>
      <div class="compose-header">
        <el-button size="small" @click="router.back()">← 返回</el-button>
        <span>发送内部邮件</span>
      </div>
    </template>

    <el-form :model="form" label-width="80px" style="max-width: 640px">
      <el-form-item label="收件人" required>
        <el-input v-model="form.to" placeholder="内部邮箱地址（多个用 ; 分隔）" />
      </el-form-item>
      <el-form-item label="主题">
        <el-input v-model="form.subject" placeholder="邮件主题" maxlength="200" />
      </el-form-item>
      <el-form-item label="正文">
        <el-input v-model="form.body" type="textarea" :rows="10" placeholder="邮件正文" />
      </el-form-item>
      <el-form-item>
        <el-button type="primary" :loading="sending" @click="onSend">发送</el-button>
        <el-button @click="router.back()">取消</el-button>
      </el-form-item>
    </el-form>
    <div class="tip">
      内部邮件经 email-service 落库（DryRun 模式，不真实外发 SMTP）；发送成功后可在「内部邮件」列表查看。
    </div>
  </el-card>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { emailsApi } from '../../api/emails'

const router = useRouter()
const sending = ref(false)
const form = reactive({
  to: '',
  subject: '',
  body: '',
})

async function onSend() {
  if (!form.to.trim()) {
    ElMessage.warning('请输入收件人')
    return
  }
  if (!form.subject.trim() && !form.body.trim()) {
    ElMessage.warning('主题与正文至少填写一项')
    return
  }
  sending.value = true
  try {
    const email = await emailsApi.send({
      to: form.to.trim(),
      subject: form.subject.trim(),
      body: form.body.trim(),
      isHtml: false,
    })
    ElMessage.success(`已发送（状态：${email.status === 1 ? '已发送' : '待发送'}）`)
    router.push('/emails')
  } catch {
    // 错误由拦截器提示
  } finally {
    sending.value = false
  }
}
</script>

<style scoped>
.compose-header {
  display: flex;
  align-items: center;
  gap: 12px;
}
.tip {
  font-size: 12px;
  color: #909399;
  margin-top: 8px;
}
</style>

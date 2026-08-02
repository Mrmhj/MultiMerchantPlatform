<template>
  <el-card shadow="never">
    <template #header>
      <div class="toolbar">
        <div>
          <el-select v-model="query.status" placeholder="状态" clearable style="width: 130px" @change="load">
            <el-option label="可见" value="visible" />
            <el-option label="已隐藏" value="hidden" />
          </el-select>
          <el-input-number v-model="query.rating" :min="1" :max="5" placeholder="评分" style="width: 110px; margin-left: 8px" @change="load" />
          <el-button type="primary" style="margin-left: 8px" @click="load">查询</el-button>
        </div>
      </div>
    </template>

    <el-table :data="list" v-loading="loading" border>
      <el-table-column prop="productName" label="商品" min-width="160" show-overflow-tooltip />
      <el-table-column prop="skuSpec" label="规格" width="100" />
      <el-table-column label="评分" width="90" align="center">
        <template #default="{ row }">
          <el-rate :model-value="row.rating" disabled size="small" />
        </template>
      </el-table-column>
      <el-table-column prop="content" label="评价内容" min-width="220" show-overflow-tooltip />
      <el-table-column label="买家" width="110">
        <template #default="{ row }">{{ row.isAnonymous ? '匿名用户' : row.displayName }}</template>
      </el-table-column>
      <el-table-column label="状态" width="90" align="center">
        <template #default="{ row }">
          <el-tag :type="row.status === 'Visible' ? 'success' : 'info'">
            {{ row.status === 'Visible' ? '可见' : '已隐藏' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="160" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openReply(row)">{{ row.replyContent ? '修改回复' : '回复' }}</el-button>
          <el-button v-if="row.status === 'Visible'" link type="warning" @click="toggleStatus(row, false)">隐藏</el-button>
          <el-button v-else link type="success" @click="toggleStatus(row, true)">恢复</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination style="margin-top: 16px; justify-content: flex-end" layout="total, prev, pager, next"
                   :total="total" :page-size="query.pageSize" v-model:current-page="query.page" @current-change="load" />

    <!-- 回复弹窗 -->
    <el-dialog v-model="replyDialog" title="回复评价" width="460px">
      <el-form label-width="80px">
        <el-form-item label="评价内容">
          <div class="review-content">{{ current?.content }}</div>
        </el-form-item>
        <el-form-item label="回复内容">
          <el-input v-model="replyText" type="textarea" :rows="3" maxlength="500" show-word-limit
                    placeholder="回复买家评价（1-500 字）" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="replyDialog = false">取消</el-button>
        <el-button type="primary" :loading="replying" @click="submitReply">提交</el-button>
      </template>
    </el-dialog>
  </el-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reviewApi, type Review } from '../../api'

const loading = ref(false)
const list = ref<Review[]>([])
const total = ref(0)
const query = reactive({ page: 1, pageSize: 20, status: '' as string, rating: undefined as number | undefined })

const current = ref<Review | null>(null)
const replyDialog = ref(false)
const replyText = ref('')
const replying = ref(false)

async function load() {
  loading.value = true
  try {
    const res = await reviewApi.merchantList({
      page: query.page, pageSize: query.pageSize,
      status: query.status || undefined, rating: query.rating,
    })
    list.value = res.items
    total.value = res.totalCount
  } finally {
    loading.value = false
  }
}

function openReply(row: Review) {
  current.value = row
  replyText.value = row.replyContent || ''
  replyDialog.value = true
}

async function submitReply() {
  if (!current.value || replyText.value.trim().length === 0) {
    ElMessage.warning('请输入回复内容')
    return
  }
  replying.value = true
  try {
    await reviewApi.reply(current.value.id, replyText.value.trim())
    ElMessage.success('回复已提交')
    replyDialog.value = false
    load()
  } finally {
    replying.value = false
  }
}

async function toggleStatus(row: Review, visible: boolean) {
  await ElMessageBox.confirm(`确认${visible ? '恢复显示' : '隐藏'}该评价？`, '提示', { type: 'warning' })
  await reviewApi.changeStatus(row.id, visible)
  ElMessage.success('操作成功')
  load()
}

onMounted(load)
</script>

<style scoped>
.toolbar { display: flex; justify-content: space-between; }
.review-content { font-size: 13px; color: #606266; line-height: 1.6; }
</style>

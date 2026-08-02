<template>
  <el-card shadow="never">
    <template #header>
      <div class="toolbar">
        <div>
          <el-select v-model="query.status" placeholder="状态" clearable style="width: 130px" @change="load">
            <el-option label="在售" value="2" />
            <el-option label="已下架" value="3" />
            <el-option label="草稿" value="1" />
          </el-select>
          <el-input v-model="query.keyword" placeholder="商品名称" clearable style="width: 200px; margin-left: 8px"
                    @keyup.enter="load" @clear="load" />
          <el-button type="primary" style="margin-left: 8px" @click="load">查询</el-button>
        </div>
        <div>
          <el-button type="primary" @click="$router.push('/products/edit')">新建商品</el-button>
        </div>
      </div>
    </template>

    <el-table :data="list" v-loading="loading" border>
      <el-table-column prop="name" label="商品名称" min-width="180" show-overflow-tooltip />
      <el-table-column prop="coverImage" label="封面" width="70">
        <template #default="{ row }">
          <el-image v-if="row.coverImage" :src="row.coverImage" style="width: 48px; height: 48px; border-radius: 4px" fit="cover" />
          <span v-else>-</span>
        </template>
      </el-table-column>
      <el-table-column label="SKU 数" width="80" align="center">
        <template #default="{ row }">{{ row.skus?.length ?? 0 }}</template>
      </el-table-column>
      <el-table-column label="价格区间（元）" width="140">
        <template #default="{ row }">
          <template v-if="row.skus?.length">
            {{ Math.min(...row.skus.map((s: any) => s.price)) }} ~ {{ Math.max(...row.skus.map((s: any) => s.price)) }}
          </template>
          <span v-else>-</span>
        </template>
      </el-table-column>
      <el-table-column label="状态" width="90" align="center">
        <template #default="{ row }">
          <el-tag :type="statusTag(row.status)">{{ statusText(row.status) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" label="创建时间" width="170">
        <template #default="{ row }">{{ fmtTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="200" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="$router.push(`/products/edit/${row.id}`)">编辑</el-button>
          <el-button v-if="row.status !== 2" link type="success" @click="changeStatus(row, 2)">上架</el-button>
          <el-button v-else link type="warning" @click="changeStatus(row, 3)">下架</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination style="margin-top: 16px; justify-content: flex-end" layout="total, prev, pager, next"
                   :total="total" :page-size="query.pageSize" v-model:current-page="query.page" @current-change="load" />
  </el-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { productApi, type Product } from '../../api'

const loading = ref(false)
const list = ref<Product[]>([])
const total = ref(0)
const query = reactive({ page: 1, pageSize: 20, status: '' as string, keyword: '' })

function statusText(s: number) { return s === 1 ? '草稿' : s === 2 ? '在售' : s === 3 ? '已下架' : '未知' }
function statusTag(s: number) { return s === 2 ? 'success' : s === 3 ? 'info' : 'warning' }
function fmtTime(t?: string) { return t ? new Date(t).toLocaleString('zh-CN') : '-' }

async function load() {
  loading.value = true
  try {
    const res = await productApi.list({
      page: query.page, pageSize: query.pageSize,
      status: query.status || undefined, keyword: query.keyword || undefined,
    })
    list.value = res.items
    total.value = res.totalCount
  } finally {
    loading.value = false
  }
}

async function changeStatus(row: Product, status: number) {
  const text = status === 2 ? '上架' : '下架'
  await ElMessageBox.confirm(`确认${text}「${row.name}」？`, '提示', { type: 'warning' })
  await productApi.updateStatus(row.id, status)
  ElMessage.success(`${text}成功`)
  load()
}

onMounted(load)
</script>

<style scoped>
.toolbar { display: flex; justify-content: space-between; }
</style>

<template>
  <el-card shadow="never">
    <template #header>
      <div class="toolbar">
        <b>商品分类</b>
        <el-button type="primary" @click="openDialog()">新增分类</el-button>
      </div>
    </template>

    <el-table :data="list" v-loading="loading" border>
      <el-table-column prop="name" label="分类名称" min-width="160" />
      <el-table-column prop="sortOrder" label="排序" width="80" align="center" />
      <el-table-column label="状态" width="90" align="center">
        <template #default="{ row }">
          <el-tag :type="row.isActive ? 'success' : 'info'">{{ row.isActive ? '启用' : '停用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="160">
        <template #default="{ row }">
          <el-button link type="primary" @click="openDialog(row)">编辑</el-button>
          <el-button link type="danger" @click="remove(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" :title="dialogForm.id ? '编辑分类' : '新增分类'" width="420px">
      <el-form :model="dialogForm" label-width="80px">
        <el-form-item label="名称">
          <el-input v-model="dialogForm.name" placeholder="分类名称" />
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="dialogForm.sortOrder" :min="0" />
        </el-form-item>
        <el-form-item label="状态">
          <el-switch v-model="dialogForm.isActive" active-text="启用" inactive-text="停用" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">保存</el-button>
      </template>
    </el-dialog>
  </el-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { productApi, type Category } from '../../api'

const loading = ref(false)
const saving = ref(false)
const list = ref<Category[]>([])
const dialogVisible = ref(false)
const dialogForm = reactive<{ id?: string; name: string; sortOrder: number; isActive: boolean }>({
  id: undefined, name: '', sortOrder: 0, isActive: true,
})

async function load() {
  loading.value = true
  try {
    list.value = await productApi.categories.list()
  } finally {
    loading.value = false
  }
}

function openDialog(row?: Category) {
  dialogForm.id = row?.id
  dialogForm.name = row?.name || ''
  dialogForm.sortOrder = row?.sortOrder ?? 0
  dialogForm.isActive = row?.isActive ?? true
  dialogVisible.value = true
}

async function save() {
  if (!dialogForm.name.trim()) {
    ElMessage.warning('请输入分类名称')
    return
  }
  saving.value = true
  try {
    if (dialogForm.id) {
      await productApi.categories.update(dialogForm.id, {
        name: dialogForm.name, sortOrder: dialogForm.sortOrder, isActive: dialogForm.isActive,
      })
      ElMessage.success('分类已更新')
    } else {
      await productApi.categories.create({
        name: dialogForm.name, sortOrder: dialogForm.sortOrder, isActive: dialogForm.isActive,
      })
      ElMessage.success('分类已创建')
    }
    dialogVisible.value = false
    load()
  } finally {
    saving.value = false
  }
}

async function remove(row: Category) {
  await ElMessageBox.confirm(`确认删除分类「${row.name}」？存在子分类或商品时无法删除。`, '提示', { type: 'warning' })
  try {
    await productApi.categories.remove(row.id)
    ElMessage.success('已删除')
    load()
  } catch {
    // 服务端 400 已由拦截器提示
  }
}

onMounted(load)
</script>

<style scoped>
.toolbar { display: flex; justify-content: space-between; }
</style>

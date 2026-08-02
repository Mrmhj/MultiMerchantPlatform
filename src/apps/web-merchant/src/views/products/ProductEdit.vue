<template>
  <el-card shadow="never">
    <template #header>
      <div class="toolbar">
        <b>{{ isEdit ? '编辑商品' : '新建商品' }}</b>
        <el-button @click="$router.back()">返回</el-button>
      </div>
    </template>

    <el-form :model="form" label-width="90px" style="max-width: 720px" :rules="rules" ref="formRef">
      <el-form-item label="商品名称" prop="name">
        <el-input v-model="form.name" placeholder="商品名称（2-100 字）" />
      </el-form-item>
      <el-form-item label="商品分类" prop="categoryId">
        <el-select v-model="form.categoryId" placeholder="选择分类" style="width: 240px">
          <el-option v-for="c in categories" :key="c.id" :label="c.name" :value="c.id" />
        </el-select>
      </el-form-item>
      <el-form-item label="封面图">
        <el-input v-model="form.coverImage" placeholder="图片 URL（选填）" />
      </el-form-item>
      <el-form-item label="商品描述">
        <el-input v-model="form.description" type="textarea" :rows="3" placeholder="商品卖点、详情描述（选填）" />
      </el-form-item>

      <el-divider content-position="left">SKU 规格（至少 1 项）</el-divider>
      <el-form-item label="SKU 列表">
        <div class="sku-wrap">
          <el-table :data="form.skus" border size="small">
            <el-table-column label="SKU 编码" min-width="140">
              <template #default="{ row }">
                <el-input v-model="row.skuCode" placeholder="如 BREAD-500G" />
              </template>
            </el-table-column>
            <el-table-column label="规格" min-width="140">
              <template #default="{ row }">
                <el-input v-model="row.spec" placeholder="如 500g" />
              </template>
            </el-table-column>
            <el-table-column label="价格（元）" width="130">
              <template #default="{ row }">
                <el-input-number v-model="row.price" :min="0.01" :precision="2" :controls="false" style="width: 110px" />
              </template>
            </el-table-column>
            <el-table-column label="初始库存" width="130">
              <template #default="{ row }">
                <el-input-number v-model="row.stock" :min="0" :precision="0" :controls="false" style="width: 110px" />
              </template>
            </el-table-column>
            <el-table-column width="70" align="center">
              <template #default="{ $index }">
                <el-button link type="danger" :disabled="form.skus.length <= 1" @click="removeSku($index)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
          <el-button style="margin-top: 8px" @click="addSku">+ 添加 SKU</el-button>
        </div>
      </el-form-item>

      <el-form-item>
        <el-button type="primary" :loading="loading" @click="onSubmit">{{ isEdit ? '保存修改' : '创建商品' }}</el-button>
      </el-form-item>
    </el-form>
  </el-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { productApi, type Category, type SkuItem } from '../../api'

const route = useRoute()
const isEdit = computed(() => !!route.params.id)
const formRef = ref<FormInstance>()
const loading = ref(false)
const categories = ref<Category[]>([])

const form = reactive({
  name: '', categoryId: '', description: '', coverImage: '',
  skus: [] as SkuItem[],
})

const rules: FormRules = {
  name: [{ required: true, min: 2, max: 100, message: '请输入商品名称', trigger: 'blur' }],
  categoryId: [{ required: true, message: '请选择商品分类', trigger: 'change' }],
}

function addSku() {
  form.skus.push({ skuCode: '', spec: '', price: 0, stock: 0 })
}

function removeSku(index: number) {
  form.skus.splice(index, 1)
}

async function loadCategories() {
  categories.value = await productApi.categories.list()
}

async function loadProduct() {
  const id = route.params.id as string
  const p = await productApi.detail(id)
  form.name = p.name
  form.categoryId = p.categoryId
  form.description = p.description || ''
  form.coverImage = p.coverImage || ''
  form.skus = p.skus.map((s) => ({ skuCode: s.skuCode, spec: s.spec, price: s.price, stock: s.stock }))
}

async function onSubmit() {
  await formRef.value?.validate()
  const skus = form.skus.filter((s) => s.skuCode.trim() && s.spec.trim())
  if (skus.length === 0) {
    ElMessage.warning('请至少填写一个完整的 SKU（编码 + 规格）')
    return
  }
  loading.value = true
  try {
    if (isEdit.value) {
      await productApi.update(route.params.id as string, {
        name: form.name, categoryId: form.categoryId,
        description: form.description || undefined, coverImage: form.coverImage || undefined,
      })
      ElMessage.success('商品信息已更新')
    } else {
      await productApi.create({
        name: form.name, categoryId: form.categoryId,
        description: form.description || undefined, coverImage: form.coverImage || undefined,
        skus,
      })
      ElMessage.success('商品创建成功（草稿状态，请上架后销售）')
    }
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  await loadCategories()
  if (isEdit.value) {
    await loadProduct()
  } else {
    addSku()
  }
})
</script>

<style scoped>
.toolbar { display: flex; justify-content: space-between; align-items: center; }
.sku-wrap { width: 100%; }
</style>

<template>
  <div class="home">
    <h2 class="page-title">全部商品</h2>
    <div v-loading="loading" class="product-grid">
      <el-card v-for="p in products" :key="p.id" class="product-card" shadow="hover" @click="$router.push(`/product/${p.id}`)">
        <div class="product-cover">
          <el-image
            :src="p.coverImage || placeholder"
            fit="cover"
            class="cover-img"
          >
            <template #error>
              <div class="cover-placeholder">{{ p.name.slice(0, 1) }}</div>
            </template>
          </el-image>
        </div>
        <div class="product-name" :title="p.name">{{ p.name }}</div>
        <div class="product-price">¥ {{ minPrice(p) }}</div>
        <div class="product-meta">{{ p.skus.length }} 个规格</div>
      </el-card>
    </div>
    <el-empty v-if="!loading && products.length === 0" description="暂无在售商品" />
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { productApi, type Product } from '../api'

const products = ref<Product[]>([])
const loading = ref(false)
const placeholder = 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="200" height="150"><rect width="200" height="150" fill="%23f0f2f5"/></svg>'

const minPrice = (p: Product) => Math.min(...p.skus.filter(s => s.isActive).map(s => s.price)).toFixed(2)

onMounted(async () => {
  loading.value = true
  try {
    const res = await productApi.list(1, 12)
    products.value = res.items
  } finally {
    loading.value = false
  }
})
</script>

<style scoped>
.page-title { margin-bottom: 16px; font-weight: 600; }
.product-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; }
.product-card { cursor: pointer; }
.product-cover { height: 180px; margin-bottom: 12px; border-radius: 6px; overflow: hidden; background: #f0f2f5; }
.cover-img { width: 100%; height: 100%; }
.cover-placeholder { display: flex; align-items: center; justify-content: center; height: 100%; font-size: 48px; color: #c0c4cc; }
.product-name { font-size: 14px; font-weight: 500; margin-bottom: 8px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.product-price { color: #e4393c; font-size: 18px; font-weight: 700; margin-bottom: 4px; }
.product-meta { font-size: 12px; color: #909399; }
@media (max-width: 900px) { .product-grid { grid-template-columns: repeat(2, 1fr); } }
</style>

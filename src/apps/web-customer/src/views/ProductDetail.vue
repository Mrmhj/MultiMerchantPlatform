<template>
  <div v-loading="loading">
    <el-card v-if="product" class="detail-card">
      <div class="detail-body">
        <div class="detail-cover">
          <el-image :src="product.coverImage || placeholder" fit="cover" class="cover-img">
            <template #error>
              <div class="cover-placeholder">{{ product.name.slice(0, 1) }}</div>
            </template>
          </el-image>
        </div>
        <div class="detail-info">
          <h1 class="name">{{ product.name }}</h1>
          <p class="desc">{{ product.description || '暂无描述' }}</p>
          <div class="price-row">
            <span class="label">价格</span>
            <span class="price">¥ {{ selectedSku ? selectedSku.price.toFixed(2) : '--' }}</span>
          </div>
          <div class="sku-row">
            <span class="label">规格</span>
            <el-radio-group v-model="skuId" class="sku-group">
              <el-radio-button v-for="s in activeSkus" :key="s.id" :value="s.id">
                {{ s.spec }}
              </el-radio-button>
            </el-radio-group>
          </div>
          <div class="qty-row">
            <span class="label">数量</span>
            <el-input-number v-model="quantity" :min="1" :max="selectedSku?.stock || 99" />
          </div>
          <div class="stock-row" v-if="selectedSku">库存 {{ selectedSku.stock }}</div>
          <div class="action-row">
            <el-button type="danger" size="large" :disabled="!selectedSku" @click="buyNow">
              立即购买
            </el-button>
          </div>
        </div>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { productApi, type Product, type SkuInfo } from '../api'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const product = ref<Product | null>(null)
const skuId = ref('')
const quantity = ref(1)
const placeholder = 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="300" height="300"><rect width="300" height="300" fill="%23f0f2f5"/></svg>'

const activeSkus = computed<SkuInfo[]>(() => (product.value?.skus || []).filter(s => s.isActive))
const selectedSku = computed(() => activeSkus.value.find(s => s.id === skuId.value))

onMounted(async () => {
  loading.value = true
  try {
    product.value = await productApi.detail(route.params.id as string)
    if (activeSkus.value.length) skuId.value = activeSkus.value[0].id
  } finally {
    loading.value = false
  }
})

function buyNow() {
  if (!product.value || !selectedSku.value) return
  router.push({
    name: 'order-submit',
    query: {
      productId: product.value.id,
      skuId: selectedSku.value.id,
      quantity: quantity.value,
    },
  })
}
</script>

<style scoped>
.detail-card { max-width: 900px; margin: 0 auto; }
.detail-body { display: flex; gap: 32px; }
.detail-cover { width: 320px; height: 320px; border-radius: 8px; overflow: hidden; background: #f0f2f5; flex-shrink: 0; }
.cover-img { width: 100%; height: 100%; }
.cover-placeholder { display: flex; align-items: center; justify-content: center; height: 100%; font-size: 80px; color: #c0c4cc; }
.detail-info { flex: 1; }
.name { font-size: 22px; margin-bottom: 8px; }
.desc { color: #606266; margin-bottom: 20px; }
.label { color: #909399; margin-right: 12px; width: 36px; display: inline-block; }
.price-row, .sku-row, .qty-row, .stock-row { margin-bottom: 16px; display: flex; align-items: center; }
.price { color: #e4393c; font-size: 26px; font-weight: 700; }
.stock-row { color: #909399; font-size: 13px; }
.action-row { margin-top: 24px; }
</style>

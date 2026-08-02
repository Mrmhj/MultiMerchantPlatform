<script setup lang="ts">
import { onLaunch } from '@dcloudio/uni-app'
import { useAuthStore } from './stores/auth'

onLaunch(() => {
  // 恢复登录态
  useAuthStore().restore()
})
</script>

<style>
/* 全局样式 */
page {
  background-color: #f5f6f7;
  font-size: 28rpx;
  color: #333;
}
</style>

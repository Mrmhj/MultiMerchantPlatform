import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import AutoImport from 'unplugin-auto-import/vite'
import Components from 'unplugin-vue-components/vite'
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers'

// 商户端 Web — Vue 3.5 + Vite 8 + TS + Element Plus（端口 5174）
export default defineConfig({
  plugins: [
    vue(),
    AutoImport({ resolvers: [ElementPlusResolver()] }),
    Components({ resolvers: [ElementPlusResolver()] }),
  ],
  server: {
    port: 5174,
    proxy: {
      // 开发环境代理到 YARP 网关（8000）
      '/api': {
        target: 'http://localhost:8000',
        changeOrigin: true,
      },
      // SignalR WebSocket 转发（网关 /hub/chat）
      '/hub': {
        target: 'http://localhost:8000',
        changeOrigin: true,
        ws: true,
      },
    },
  },
})

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import AutoImport from 'unplugin-auto-import/vite'
import Components from 'unplugin-vue-components/vite'
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers'

// 桌面端（Electron 渲染进程）— Vue 3.5 + Vite 8 + TS + Element Plus（dev 端口 5176）
// 开发环境经 Vite 代理到 YARP 网关（8000）；打包产物供 Electron 主进程 loadFile 加载
export default defineConfig({
  plugins: [
    vue(),
    AutoImport({ resolvers: [ElementPlusResolver()] }),
    Components({ resolvers: [ElementPlusResolver()] }),
  ],
  // Electron file:// 加载需相对路径
  base: './',
  server: {
    port: 5176,
    strictPort: true,
    proxy: {
      // 开发环境代理到 YARP 网关（8000）
      '/api': {
        target: 'http://localhost:8000',
        changeOrigin: true,
      },
      // SignalR WebSocket 转发（网关 /hub/notification）
      '/hub': {
        target: 'http://localhost:8000',
        changeOrigin: true,
        ws: true,
      },
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
})

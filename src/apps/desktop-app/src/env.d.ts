/// <reference types="vite/client" />

declare module '*.vue' {
  import type { DefineComponent } from 'vue'
  const component: DefineComponent<{}, {}, any>
  export default component
}

// Electron preload 暴露的桌面端 API（window.desktop）
interface Window {
  desktop?: {
    appInfo: {
      platform: string
      electron: string
    }
  }
}

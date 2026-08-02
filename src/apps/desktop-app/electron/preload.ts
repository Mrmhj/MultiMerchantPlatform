import { contextBridge } from 'electron'

// 预加载脚本 — contextIsolation 下向渲染进程暴露最小安全 API（不暴露 Node 能力）
contextBridge.exposeInMainWorld('desktop', {
  /** 桌面端应用信息（平台/版本） */
  appInfo: {
    platform: process.platform,
    electron: process.versions.electron,
  },
})

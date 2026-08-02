import { app, BrowserWindow, shell } from 'electron'
import path from 'node:path'

// 开发模式：渲染进程由 Vite dev server 提供（npm run dev 注入 VITE_DEV_SERVER_URL）
const devServerUrl = process.env.VITE_DEV_SERVER_URL

function createWindow(): void {
  const win = new BrowserWindow({
    width: 1280,
    height: 800,
    minWidth: 1024,
    minHeight: 700,
    title: '摩登商户工作台',
    autoHideMenuBar: true,
    backgroundColor: '#f5f7fa',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  })

  if (devServerUrl) {
    void win.loadURL(devServerUrl)
  } else {
    void win.loadFile(path.join(__dirname, '../dist/index.html'))
  }

  // 冒烟可验证：渲染进程完成加载后输出到主进程日志
  win.webContents.on('did-finish-load', () => {
    console.log(`[desktop-app] renderer loaded: ${win.webContents.getTitle()}`)
  })

  // 渲染进程异常（白屏/JS 崩溃）输出到主进程日志
  win.webContents.on('render-process-gone', (_event, details) => {
    console.error(`[desktop-app] renderer gone: ${details.reason}`)
  })

  // 站外链接一律交给系统浏览器，不在应用内新开窗口
  win.webContents.setWindowOpenHandler(({ url }) => {
    void shell.openExternal(url)
    return { action: 'deny' }
  })
}

app.whenReady().then(() => {
  createWindow()

  // macOS：点击 Dock 图标且无窗口时重建
  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow()
  })
})

// 除 macOS 外，全部窗口关闭即退出应用
app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit()
})

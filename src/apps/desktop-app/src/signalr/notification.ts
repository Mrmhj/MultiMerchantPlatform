import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import type { Announcement } from '../api/announcements'
import type { NotificationItem } from '../api/notifications'

// SignalR 通知客户端 — 连接网关 /hub/notification（WebSocket 不能带 Authorization 头，
// 令牌经 query access_token 传递，与后端 JwtBearer OnMessageReceived 约定一致）
let connection: HubConnection | null = null

/** 建立通知实时通道（登录后调用） */
export function connectNotificationHub(token: string): HubConnection {
  if (connection) return connection

  connection = new HubConnectionBuilder()
    .withUrl(`/hub/notification?access_token=${encodeURIComponent(token)}`)
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()

  void connection.start().catch(() => {
    // 服务未就绪时静默，由上层轮询兜底；连接中断由 withAutomaticReconnect 自动恢复
  })

  return connection
}

/** 断开通知实时通道（登出时调用） */
export function disconnectNotificationHub(): void {
  if (!connection) return
  void connection.stop()
  connection = null
}

/** 注册事件回调（返回注销函数） */
export function onReceiveNotification(cb: (n: NotificationItem) => void): () => void {
  return register('ReceiveNotification', cb)
}

export function onUnreadCountChanged(cb: (count: number) => void): () => void {
  return register('UnreadCountChanged', cb)
}

export function onReceiveAnnouncement(cb: (a: Announcement) => void): () => void {
  return register('ReceiveAnnouncement', cb)
}

function register<T>(event: string, cb: (arg: T) => void): () => void {
  if (!connection) return () => undefined
  connection.on(event, cb)
  return () => connection?.off(event, cb)
}

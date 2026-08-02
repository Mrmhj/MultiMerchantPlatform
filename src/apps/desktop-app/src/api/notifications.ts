import http from './http'
import type { PagedResult, UnreadCount } from './announcements'

// ── 站内信 DTO ────────────────────────────────────────────
export type NotificationType = 1 | 2 | 3 | 4 | 5 | 6 | 7 // Order/Payment/Logistics/Promotion/System/Risk/Monitor

export interface NotificationItem {
  id: string
  type: NotificationType
  title: string
  content: string
  bizType: string | null
  bizId: string | null
  isRead: boolean
  readAt: string | null
  createdAt: string
}

// ── 站内信 API（通知中心收件箱）────────────────────────────
export const notificationsApi = {
  /** 我的通知分页列表 */
  list(type?: NotificationType, isRead?: boolean, page = 1, pageSize = 20) {
    return http.get('/notifications', {
      params: { type, isRead, page, pageSize },
    }) as Promise<PagedResult<NotificationItem>>
  },

  /** 未读通知数 */
  unreadCount() {
    return http.get('/notifications/unread-count') as Promise<UnreadCount>
  },

  /** 标记单条已读 */
  markRead(id: string) {
    return http.post(`/notifications/${id}/read`) as Promise<NotificationItem>
  },

  /** 全部标记已读 */
  markAllRead() {
    return http.post('/notifications/read-all') as Promise<UnreadCount>
  },

  /** 删除单条（软删除） */
  remove(id: string) {
    return http.delete(`/notifications/${id}`)
  },
}

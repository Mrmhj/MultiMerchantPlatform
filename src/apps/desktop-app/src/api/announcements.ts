import http from './http'

// ── 公告 DTO ──────────────────────────────────────────────
export type AnnouncementCategory = 1 | 2 | 3 // System / Operation / Maintenance
export type AnnouncementStatus = 0 | 1 | 2 // Draft / Published / Offline

export interface Announcement {
  id: string
  title: string
  content: string
  category: AnnouncementCategory
  publisherName: string
  status: AnnouncementStatus
  publishedAt: string | null
  isRead: boolean
  readAt: string | null
  createdAt: string
}

export interface PagedResult<T> {
  totalCount: number
  page: number
  pageSize: number
  items: T[]
}

export interface UnreadCount {
  unreadCount: number
}

// ── 公告 API ──────────────────────────────────────────────
export const announcementsApi = {
  /** 公告分页列表（含当前用户已读状态） */
  list(category?: AnnouncementCategory, page = 1, pageSize = 20) {
    return http.get('/notifications/announcements', {
      params: { category, page, pageSize },
    }) as Promise<PagedResult<Announcement>>
  },

  /** 公告详情 */
  detail(id: string) {
    return http.get(`/notifications/announcements/${id}`) as Promise<Announcement>
  },

  /** 公告未读数（顶栏角标） */
  unreadCount() {
    return http.get('/notifications/announcements/unread-count') as Promise<UnreadCount>
  },

  /** 标记公告已读 */
  markRead(id: string) {
    return http.post(`/notifications/announcements/${id}/read`) as Promise<Announcement>
  },
}

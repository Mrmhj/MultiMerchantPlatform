import http from './http'
import type { PagedResult } from './announcements'

// ── 邮件 DTO ──────────────────────────────────────────────
export type EmailStatus = 0 | 1 | 2 | 3 // Pending / Sent / Failed / DeadLetter

export interface EmailItem {
  id: string
  from: string
  to: string
  subject: string
  body: string | null
  isHtml: boolean
  templateName: string | null
  status: EmailStatus
  retryCount: number
  maxRetryCount: number
  sentAt: string | null
  lastError: string | null
  createdAt: string
}

export interface SendEmailRequest {
  to: string
  subject?: string
  body?: string
  templateName?: string
  templateData?: Record<string, unknown>
  isHtml?: boolean
  cc?: string
  bcc?: string
  maxRetryCount?: number
}

// ── 邮件 API（email-service，内部邮件中心）────────────────
export const emailsApi = {
  /** 分页查询邮件（全部/按状态过滤） */
  list(status?: EmailStatus, to?: string, page = 1, pageSize = 20) {
    return http.get('/emails', {
      params: { status, to, page, pageSize },
    }) as Promise<PagedResult<EmailItem>>
  },

  /** 邮件详情 */
  detail(id: string) {
    return http.get(`/emails/${id}`) as Promise<EmailItem>
  },

  /** 发送内部邮件（DryRun 模式落库，不发外部 SMTP） */
  send(request: SendEmailRequest) {
    return http.post('/emails', request) as Promise<EmailItem>
  },

  /** 手动重试失败/死信邮件 */
  retry(id: string) {
    return http.post(`/emails/${id}/retry`) as Promise<EmailItem>
  },
}

import api from '../lib/api'
import type { Webhook } from '../types/webhook'

/** One recorded delivery attempt-set (mirrors WebhookDeliveryDto). */
export interface WebhookDelivery {
  id: string
  event: string
  success: boolean
  statusCode: number | null
  error: string | null
  attempts: number
  createdAt: string
}

/** REST calls for managing outgoing webhooks. */
export const webhookService = {
  getWebhooks(): Promise<Webhook[]> {
    return api.get<Webhook[]>('/api/webhooks').then((r) => r.data)
  },

  createWebhook(data: { url: string; event: string }): Promise<Webhook> {
    return api.post<Webhook>('/api/webhooks', data).then((r) => r.data)
  },

  deleteWebhook(id: string): Promise<void> {
    return api.delete(`/api/webhooks/${id}`).then(() => undefined)
  },

  /** Pauses or resumes a webhook (paused ones receive no events). */
  setActive(id: string, isActive: boolean): Promise<Webhook> {
    return api.put<Webhook>(`/api/webhooks/${id}/active`, { isActive }).then((r) => r.data)
  },

  /** Sends a sample payload and returns the delivery outcome. */
  testWebhook(id: string): Promise<WebhookDelivery> {
    return api.post<WebhookDelivery>(`/api/webhooks/${id}/test`).then((r) => r.data)
  },

  /** Recent deliveries for a webhook (newest first). */
  getDeliveries(id: string): Promise<WebhookDelivery[]> {
    return api.get<WebhookDelivery[]>(`/api/webhooks/${id}/deliveries`).then((r) => r.data)
  },
}

import api from '../lib/api'
import type { Webhook } from '../types/webhook'

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
}

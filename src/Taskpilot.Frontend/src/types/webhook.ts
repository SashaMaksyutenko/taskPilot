/** A webhook (mirrors the backend WebhookDto). */
export interface Webhook {
  id: string
  url: string
  event: string
  secret: string
  isActive: boolean
  createdAt: string
}

/** Events the user can subscribe to (matches WebhookEvents on the backend). */
export const WEBHOOK_EVENTS = ['task.completed', 'task.created', 'project.created'] as const

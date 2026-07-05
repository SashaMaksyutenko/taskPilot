import api from '../lib/api'
import type { AppNotification } from '../types/notification'

/** REST calls for in-app notifications. */
export const notificationService = {
  getNotifications(unreadOnly = false): Promise<AppNotification[]> {
    return api
      .get<AppNotification[]>('/api/notifications', { params: { unreadOnly } })
      .then((r) => r.data)
  },

  getUnreadCount(): Promise<number> {
    return api.get<{ count: number }>('/api/notifications/unread-count').then((r) => r.data.count)
  },

  markRead(id: string): Promise<void> {
    return api.post(`/api/notifications/${id}/read`).then(() => undefined)
  },

  markAllRead(): Promise<void> {
    return api.post('/api/notifications/read-all').then(() => undefined)
  },

  /** Notification types the user muted, per channel. */
  getPreferences(): Promise<{ disabledTypes: string[]; disabledEmailTypes: string[] }> {
    return api
      .get<{ disabledTypes: string[]; disabledEmailTypes: string[] }>('/api/notifications/preferences')
      .then((r) => r.data)
  },

  /** Replaces the user's muted notification types for both channels. */
  updatePreferences(
    disabledTypes: string[],
    disabledEmailTypes: string[],
  ): Promise<{ disabledTypes: string[]; disabledEmailTypes: string[] }> {
    return api
      .put<{ disabledTypes: string[]; disabledEmailTypes: string[] }>('/api/notifications/preferences', {
        disabledTypes,
        disabledEmailTypes,
      })
      .then((r) => r.data)
  },
}

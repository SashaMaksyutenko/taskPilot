import api from '../lib/api'
import type { AppNotification } from '../types/notification'

/**
 * A quiet-hours window in the user's local hours. Inside it, email/Telegram/Viber/push
 * are held back; in-app notifications still arrive.
 */
export interface QuietHours {
  enabled: boolean
  start: number
  end: number
  timeZoneId: string | null
}

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

  /** Notification types the user muted, per channel, plus digest cadence and quiet hours. */
  getPreferences(): Promise<{
    disabledTypes: string[]
    disabledEmailTypes: string[]
    digestFrequency: string
    quietHours: QuietHours
  }> {
    return api
      .get<{
        disabledTypes: string[]
        disabledEmailTypes: string[]
        digestFrequency: string
        quietHours: QuietHours
      }>('/api/notifications/preferences')
      .then((r) => r.data)
  },

  /** Sets the quiet-hours window (out-of-band channels stay silent inside it). */
  updateQuietHours(data: QuietHours): Promise<QuietHours> {
    return api.put<QuietHours>('/api/notifications/quiet-hours', data).then((r) => r.data)
  },

  /** Sets how often the user receives a digest email (Off/Daily/Weekly). */
  updateDigest(frequency: string): Promise<{ digestFrequency: string }> {
    return api
      .put<{ digestFrequency: string }>('/api/notifications/digest', { frequency })
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

  /** Whether the user has linked Telegram, plus the bot username. */
  getTelegramStatus(): Promise<{ linked: boolean; botUsername: string }> {
    return api.get<{ linked: boolean; botUsername: string }>('/api/notifications/telegram').then((r) => r.data)
  },

  /** Generates a one-time Telegram link code. */
  createTelegramLinkCode(): Promise<{ code: string; botUsername: string }> {
    return api
      .post<{ code: string; botUsername: string }>('/api/notifications/telegram/link-code')
      .then((r) => r.data)
  },

  /** Unlinks the user's Telegram. */
  unlinkTelegram(): Promise<void> {
    return api.delete('/api/notifications/telegram').then(() => undefined)
  },

  /** Whether the user has linked Viber, plus the bot name. */
  getViberStatus(): Promise<{ linked: boolean; botName: string }> {
    return api.get<{ linked: boolean; botName: string }>('/api/notifications/viber').then((r) => r.data)
  },

  /** Generates a one-time Viber link code. */
  createViberLinkCode(): Promise<{ code: string; botName: string }> {
    return api
      .post<{ code: string; botName: string }>('/api/notifications/viber/link-code')
      .then((r) => r.data)
  },

  /** Unlinks the user's Viber. */
  unlinkViber(): Promise<void> {
    return api.delete('/api/notifications/viber').then(() => undefined)
  },

  /** Public VAPID key the browser needs to subscribe to Web Push (empty when disabled). */
  getVapidKey(): Promise<{ publicKey: string }> {
    return api.get<{ publicKey: string }>('/api/notifications/push/vapid-key').then((r) => r.data)
  },

  /** Registers this browser for Web Push. */
  pushSubscribe(sub: { endpoint: string; p256dh: string; auth: string }): Promise<void> {
    return api.post('/api/notifications/push/subscribe', sub).then(() => undefined)
  },

  /** Removes this browser's Web Push subscription. */
  pushUnsubscribe(endpoint: string): Promise<void> {
    return api.post('/api/notifications/push/unsubscribe', { endpoint }).then(() => undefined)
  },
}

/** An in-app notification (mirrors the backend NotificationDto). */
export interface AppNotification {
  id: string
  type: string
  message: string
  link: string | null
  isRead: boolean
  createdAt: string
}

import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { createNotificationConnection } from '../lib/notificationHub'
import { notificationService } from '../services/notificationService'
import type { AppNotification } from '../types/notification'

/**
 * Encapsulates all in-app notification state and behaviour: the unread count,
 * the dropdown list, real-time delivery over SignalR, toast popups and the
 * read/navigation actions. Keeps the Navbar focused on rendering.
 */
export function useNotifications() {
  const navigate = useNavigate()

  const [unread, setUnread] = useState(0)
  const [notes, setNotes] = useState<AppNotification[]>([])
  const [toasts, setToasts] = useState<AppNotification[]>([])
  const [open, setOpen] = useState(false)
  const bellRef = useRef<HTMLDivElement | null>(null)

  // Initial unread count.
  useEffect(() => {
    notificationService.getUnreadCount().then(setUnread).catch(() => {})
  }, [])

  const dismissToast = (id: string) => setToasts((prev) => prev.filter((t) => t.id !== id))

  // Real-time delivery: bump the count, prepend to the list and pop a toast.
  useEffect(() => {
    const connection = createNotificationConnection()
    connection.on('ReceiveNotification', (n: AppNotification) => {
      setUnread((c) => c + 1)
      setNotes((prev) => [n, ...prev])
      setToasts((prev) => [...prev, n])
      // Auto-dismiss the toast after a few seconds.
      setTimeout(() => setToasts((prev) => prev.filter((t) => t.id !== n.id)), 5000)
    })
    connection.start().catch(() => {})
    return () => {
      connection.stop()
    }
  }, [])

  // Close the dropdown when clicking outside of it.
  useEffect(() => {
    if (!open) return
    const onClick = (e: MouseEvent) => {
      if (bellRef.current && !bellRef.current.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onClick)
    return () => document.removeEventListener('mousedown', onClick)
  }, [open])

  const toggle = () => {
    const next = !open
    setOpen(next)
    // Load the latest notifications each time the panel opens.
    if (next) notificationService.getNotifications().then(setNotes).catch(() => {})
  }

  // Marks a notification read locally (and on the server) without other side effects.
  const markReadLocally = async (n: AppNotification) => {
    if (n.isRead) return
    await notificationService.markRead(n.id).catch(() => {})
    setNotes((prev) => prev.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)))
    setUnread((c) => Math.max(0, c - 1))
  }

  const openNotification = async (n: AppNotification) => {
    await markReadLocally(n)
    setOpen(false)
    if (n.link) navigate(n.link)
  }

  const openToast = async (n: AppNotification) => {
    dismissToast(n.id)
    await markReadLocally(n)
    if (n.link) navigate(n.link)
  }

  const markAllRead = async () => {
    await notificationService.markAllRead().catch(() => {})
    setNotes((prev) => prev.map((x) => ({ ...x, isRead: true })))
    setUnread(0)
  }

  return {
    unread,
    notes,
    toasts,
    open,
    bellRef,
    toggle,
    openNotification,
    openToast,
    dismissToast,
    markAllRead,
  }
}

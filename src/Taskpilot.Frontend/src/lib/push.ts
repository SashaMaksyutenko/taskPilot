import { notificationService } from '../services/notificationService'

/** Converts a base64url VAPID key to the ArrayBuffer PushManager expects. */
function urlBase64ToBuffer(base64: string): ArrayBuffer {
  const padding = '='.repeat((4 - (base64.length % 4)) % 4)
  const b64 = (base64 + padding).replace(/-/g, '+').replace(/_/g, '/')
  const raw = atob(b64)
  const buffer = new ArrayBuffer(raw.length)
  const arr = new Uint8Array(buffer)
  for (let i = 0; i < raw.length; i++) arr[i] = raw.charCodeAt(i)
  return buffer
}

/** Whether this browser supports the Push API and service workers. */
export function pushSupported(): boolean {
  return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window
}

/** True when this browser already has an active push subscription. */
export async function getPushEnabled(): Promise<boolean> {
  if (!pushSupported()) return false
  const reg = await navigator.serviceWorker.getRegistration()
  const sub = await reg?.pushManager.getSubscription()
  return !!sub
}

/**
 * Registers the service worker, asks for permission, subscribes to Web Push and
 * sends the subscription to the backend. Throws a coded error the caller can map:
 * 'unsupported' | 'not-configured' | 'denied'.
 */
export async function enablePush(): Promise<void> {
  if (!pushSupported()) throw new Error('unsupported')

  const { publicKey } = await notificationService.getVapidKey()
  if (!publicKey) throw new Error('not-configured')

  const permission = await Notification.requestPermission()
  if (permission !== 'granted') throw new Error('denied')

  const reg = await navigator.serviceWorker.register('/sw.js')
  await navigator.serviceWorker.ready

  const sub = await reg.pushManager.subscribe({
    userVisibleOnly: true,
    applicationServerKey: urlBase64ToBuffer(publicKey),
  })

  const json = sub.toJSON()
  await notificationService.pushSubscribe({
    endpoint: sub.endpoint,
    p256dh: json.keys?.p256dh ?? '',
    auth: json.keys?.auth ?? '',
  })
}

/** Unsubscribes this browser from Web Push (backend + browser). */
export async function disablePush(): Promise<void> {
  const reg = await navigator.serviceWorker.getRegistration()
  const sub = await reg?.pushManager.getSubscription()
  if (sub) {
    await notificationService.pushUnsubscribe(sub.endpoint).catch(() => {})
    await sub.unsubscribe()
  }
}

/* TaskPilot service worker: makes the app installable (PWA), shows Web Push
   notifications and opens their link on click. */

// Bump the version to force the offline page to refresh on the next visit.
const CACHE = 'taskpilot-v1'
const OFFLINE_URL = '/offline.html'

// Pre-cache the small offline fallback page so navigations still show something useful
// when the network is down. (Full offline support would need Workbox precaching of the
// hashed app bundle — out of scope here.)
self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE).then((cache) => cache.addAll([OFFLINE_URL, '/logo-mark.svg'])),
  )
  self.skipWaiting()
})

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k)))),
  )
  self.clients.claim()
})

// Network-first for page navigations; fall back to the cached offline page when offline.
// Everything else is left to the browser (no interference with API calls or assets).
self.addEventListener('fetch', (event) => {
  if (event.request.mode !== 'navigate') return
  event.respondWith(
    fetch(event.request).catch(() => caches.match(OFFLINE_URL).then((r) => r || Response.error())),
  )
})

self.addEventListener('push', (event) => {
  let data = {}
  try {
    data = event.data ? event.data.json() : {}
  } catch {
    data = { body: event.data ? event.data.text() : '' }
  }

  const title = data.title || 'TaskPilot'
  const options = {
    body: data.body || '',
    icon: '/logo-mark.svg',
    badge: '/logo-mark.svg',
    data: { url: data.url || '/' },
  }
  event.waitUntil(self.registration.showNotification(title, options))
})

self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  const url = (event.notification.data && event.notification.data.url) || '/'
  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((windows) => {
      // Focus an existing tab if one is open, otherwise open a new one.
      for (const client of windows) {
        if ('focus' in client) {
          client.navigate(url)
          return client.focus()
        }
      }
      return self.clients.openWindow(url)
    }),
  )
})

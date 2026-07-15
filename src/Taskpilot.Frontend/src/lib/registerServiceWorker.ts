/**
 * Registers the service worker on load so the app is installable (PWA) and can show the
 * offline fallback. Push notifications reuse the same registration (see lib/push.ts).
 * A no-op when the browser has no service-worker support or during local dev over http.
 */
export function registerServiceWorker() {
  if (!('serviceWorker' in navigator)) return

  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch(() => {
      // Registration failing (e.g. private mode) must never break the app.
    })
  })
}

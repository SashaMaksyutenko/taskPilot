import type { ReactNode } from 'react'
import { Outlet } from 'react-router-dom'
import Sidebar from './Sidebar'
import TopBar from './TopBar'
import { useNotifications } from '../../hooks/useNotifications'

/**
 * Authenticated app chrome: fixed sidebar, top bar and scrollable main area.
 * Used as a React Router layout route for all logged-in pages.
 */
export default function AppShell({ children }: { children?: ReactNode }) {
  const notifications = useNotifications()

  return (
    <div className="flex min-h-screen bg-canvas">
      <Sidebar />

      <div className="flex min-w-0 flex-1 flex-col lg:pl-64">
        <TopBar notifications={notifications} />

        <main className="flex-1 px-4 py-6 sm:px-6 lg:px-8">
          {children ?? <Outlet />}
        </main>
      </div>

      {/* Real-time notification toasts */}
      {notifications.toasts.length > 0 && (
        <div className="fixed bottom-4 right-4 z-50 flex w-80 flex-col gap-2">
          {notifications.toasts.map((n) => (
            <div
              key={n.id}
              className="flex items-start gap-2 rounded-xl border border-border bg-surface p-3 shadow-elevated"
            >
              <span className="mt-1.5 h-2 w-2 flex-none rounded-full bg-accent" />
              <button
                onClick={() => notifications.openToast(n)}
                className="min-w-0 flex-1 text-left text-sm text-foreground"
              >
                {n.message}
              </button>
              <button
                onClick={() => notifications.dismissToast(n.id)}
                className="flex-none text-muted hover:text-foreground"
                aria-label="Dismiss"
              >
                ✕
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

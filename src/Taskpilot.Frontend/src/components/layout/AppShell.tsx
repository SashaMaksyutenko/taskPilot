import type { ReactNode } from 'react'
import { Outlet, useLocation } from 'react-router-dom'
import { AnimatePresence, motion } from 'framer-motion'
import Sidebar from './Sidebar'
import TopBar from './TopBar'
import ShortcutsHelp from '../ShortcutsHelp'
import CommandPalette from '../CommandPalette'
import { useNotifications } from '../../hooks/useNotifications'
import { useShortcuts } from '../../hooks/useShortcuts'

/**
 * Authenticated app chrome: fixed sidebar, top bar and scrollable main area.
 * Used as a React Router layout route for all logged-in pages.
 */
export default function AppShell({ children }: { children?: ReactNode }) {
  const notifications = useNotifications()
  const location = useLocation()
  const shortcuts = useShortcuts()

  return (
    <div className="flex min-h-screen bg-canvas">
      <ShortcutsHelp open={shortcuts.helpOpen} onClose={() => shortcuts.setHelpOpen(false)} />
      <CommandPalette open={shortcuts.paletteOpen} onClose={() => shortcuts.setPaletteOpen(false)} />
      <Sidebar />

      <div className="flex min-w-0 flex-1 flex-col lg:pl-64">
        <TopBar notifications={notifications} onOpenPalette={() => shortcuts.setPaletteOpen(true)} />

        <main className="flex-1 px-4 py-6 sm:px-6 lg:px-8">
          {/* Subtle cross-fade between routes; keyed by path so each page animates in. */}
          <AnimatePresence mode="wait">
            <motion.div
              key={location.pathname}
              initial={{ opacity: 0, y: 6 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -6 }}
              transition={{ duration: 0.18, ease: 'easeOut' }}
            >
              {children ?? <Outlet />}
            </motion.div>
          </AnimatePresence>
        </main>
      </div>

      {/* Real-time notification toasts */}
      <div className="fixed bottom-4 right-4 z-50 flex w-80 flex-col gap-2">
        <AnimatePresence initial={false}>
          {notifications.toasts.map((n) => (
            <motion.div
              key={n.id}
              layout
              initial={{ opacity: 0, x: 24, scale: 0.96 }}
              animate={{ opacity: 1, x: 0, scale: 1 }}
              exit={{ opacity: 0, x: 24, scale: 0.96 }}
              transition={{ duration: 0.2, ease: 'easeOut' }}
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
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </div>
  )
}

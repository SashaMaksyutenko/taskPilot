import {
  Bell,
  LogOut,
  Menu,
  Moon,
  Sun,
  X,
} from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { NavLink, useNavigate } from 'react-router-dom'
import Avatar from '../Avatar'
import { SidebarNav } from './Sidebar'
import { cn } from '../../lib/cn'
import { useNotifications } from '../../hooks/useNotifications'
import { logout } from '../../store/authSlice'
import { useAppDispatch, useAppSelector } from '../../store/hooks'

type NotificationState = ReturnType<typeof useNotifications>

/** Compact top bar: mobile menu, theme/lang toggles, notifications, user. */
export default function TopBar({ notifications }: { notifications: NotificationState }) {
  const dispatch = useAppDispatch()
  const navigate = useNavigate()
  const { t, i18n } = useTranslation()
  const user = useAppSelector((s) => s.auth.user)
  const [dark, setDark] = useState(() => document.documentElement.classList.contains('dark'))
  const [mobileOpen, setMobileOpen] = useState(false)

  const toggleTheme = () => {
    const next = !dark
    setDark(next)
    document.documentElement.classList.toggle('dark', next)
    localStorage.setItem('theme', next ? 'dark' : 'light')
  }

  const toggleLang = () => i18n.changeLanguage(i18n.language.startsWith('uk') ? 'en' : 'uk')

  const handleLogout = () => {
    dispatch(logout())
    navigate('/login')
  }

  return (
    <>
      <header className="sticky top-0 z-20 flex h-16 items-center gap-3 border-b border-border bg-surface/90 px-4 backdrop-blur-md sm:px-6">
        <button
          type="button"
          onClick={() => setMobileOpen(true)}
          className="rounded-lg p-2 text-muted hover:bg-canvas lg:hidden"
          aria-label="Open menu"
        >
          <Menu className="h-5 w-5" />
        </button>

        <NavLink to="/" className="flex items-center gap-2 font-bold text-foreground lg:hidden">
          <img src="/logo-mark.svg" alt="" className="h-7 w-7" />
          TaskPilot
        </NavLink>

        <div className="ml-auto flex items-center gap-1 sm:gap-2">
          <button
            type="button"
            onClick={toggleLang}
            className="rounded-lg px-2.5 py-2 text-xs font-bold text-muted hover:bg-canvas"
          >
            {i18n.language.startsWith('uk') ? 'УКР' : 'EN'}
          </button>

          <button
            type="button"
            onClick={toggleTheme}
            className="rounded-lg p-2 text-muted hover:bg-canvas"
            aria-label="Toggle theme"
          >
            {dark ? <Sun className="h-[18px] w-[18px]" /> : <Moon className="h-[18px] w-[18px]" />}
          </button>

          <div className="relative" ref={notifications.bellRef}>
            <button
              type="button"
              onClick={notifications.toggle}
              className="relative rounded-lg p-2 text-muted hover:bg-canvas"
              aria-label="Notifications"
            >
              <Bell className="h-[18px] w-[18px]" />
              {notifications.unread > 0 && (
                <span className="absolute right-0.5 top-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-red-500 px-1 text-[10px] font-bold text-white">
                  {notifications.unread}
                </span>
              )}
            </button>

            {notifications.open && (
              <div className="absolute right-0 top-full z-30 mt-2 w-80 overflow-hidden rounded-xl border border-border bg-surface shadow-elevated">
                <div className="flex items-center justify-between border-b border-border px-4 py-3">
                  <span className="text-sm font-bold">{t('dashboard.recentActivity', 'Notifications')}</span>
                  {notifications.unread > 0 && (
                    <button
                      type="button"
                      onClick={notifications.markAllRead}
                      className="text-xs font-semibold text-primary hover:underline"
                    >
                      {t('dashboard.markAllRead')}
                    </button>
                  )}
                </div>
                <div className="max-h-96 overflow-y-auto">
                  {notifications.notes.length === 0 ? (
                    <p className="px-4 py-8 text-center text-sm text-muted">{t('dashboard.noActivity')}</p>
                  ) : (
                    notifications.notes.map((n) => (
                      <button
                        key={n.id}
                        type="button"
                        onClick={() => notifications.openNotification(n)}
                        className={cn(
                          'flex w-full gap-2 border-b border-border/60 px-4 py-3 text-left text-sm last:border-b-0 hover:bg-canvas',
                          !n.isRead && 'bg-primary-muted/40',
                        )}
                      >
                        <span
                          className={cn(
                            'mt-1.5 h-2 w-2 flex-none rounded-full',
                            n.isRead ? 'bg-transparent' : 'bg-accent',
                          )}
                        />
                        <span className="min-w-0">
                          <span className="block text-foreground">{n.message}</span>
                          <span className="mt-0.5 block text-xs text-muted">
                            {new Date(n.createdAt).toLocaleString()}
                          </span>
                        </span>
                      </button>
                    ))
                  )}
                </div>
              </div>
            )}
          </div>

          <NavLink
            to="/settings"
            className="hidden items-center gap-2 rounded-lg px-2 py-1.5 text-sm text-muted hover:bg-canvas sm:flex"
          >
            <Avatar name={user?.name ?? '?'} src={user?.avatarUrl} size={28} />
            <span className="max-w-[8rem] truncate font-medium text-foreground">{user?.name}</span>
          </NavLink>

          <button
            type="button"
            onClick={handleLogout}
            className="rounded-lg p-2 text-muted hover:bg-canvas hover:text-foreground"
            title={t('nav.logout')}
          >
            <LogOut className="h-[18px] w-[18px]" />
          </button>
        </div>
      </header>

      {/* Mobile drawer */}
      {mobileOpen && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <button
            type="button"
            className="absolute inset-0 bg-black/40"
            onClick={() => setMobileOpen(false)}
            aria-label="Close menu"
          />
          <div className="absolute inset-y-0 left-0 flex w-72 flex-col bg-surface shadow-elevated">
            <div className="flex h-16 items-center justify-between border-b border-border px-4">
              <div className="flex items-center gap-2">
                <img src="/logo-mark.svg" alt="" className="h-8 w-8" />
                <span className="font-bold">TaskPilot</span>
              </div>
              <button type="button" onClick={() => setMobileOpen(false)} className="rounded-lg p-2 hover:bg-canvas">
                <X className="h-5 w-5" />
              </button>
            </div>
            <div className="flex-1 overflow-y-auto p-3">
              <SidebarNav onNavigate={() => setMobileOpen(false)} />
            </div>
          </div>
        </div>
      )}
    </>
  )
}

import {
  Bookmark,
  Bot,
  Calendar,
  FolderKanban,
  LayoutDashboard,
  MessageSquare,
  NotebookPen,
  Search,
  Settings,
  Shield,
  ShoppingBag,
  MessagesSquare,
} from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { NavLink } from 'react-router-dom'
import { cn } from '../../lib/cn'
import { useAppSelector } from '../../store/hooks'

const LINKS = [
  { to: '/', key: 'nav.dashboard', icon: LayoutDashboard, end: true },
  { to: '/projects', key: 'nav.projects', icon: FolderKanban, end: false },
  { to: '/calendar', key: 'nav.calendar', icon: Calendar, end: false },
  { to: '/forum', key: 'nav.forum', icon: MessagesSquare, end: false },
  { to: '/marketplace', key: 'nav.market', icon: ShoppingBag, end: false },
  { to: '/chat', key: 'nav.chat', icon: MessageSquare, end: false },
  { to: '/assistant', key: 'nav.assistant', icon: Bot, end: false },
  { to: '/notes', key: 'nav.notes', icon: NotebookPen, end: false },
  { to: '/bookmarks', key: 'nav.bookmarks', icon: Bookmark, end: false },
  { to: '/search', key: 'nav.search', icon: Search, end: false },
] as const

/** Reusable sidebar navigation links (desktop sidebar + mobile drawer). */
export function SidebarNav({ onNavigate }: { onNavigate?: () => void }) {
  const { t } = useTranslation()
  const user = useAppSelector((s) => s.auth.user)
  const links = user?.role === 'Admin'
    ? [...LINKS, { to: '/admin', key: 'nav.admin', icon: Shield, end: false as const }]
    : LINKS

  return (
    <nav className="space-y-0.5">
      {links.map(({ to, key, icon: Icon, end }) => (
        <NavLink
          key={to}
          to={to}
          end={end}
          onClick={onNavigate}
          className={({ isActive }) =>
            cn(
              'flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition',
              isActive
                ? 'bg-primary-muted text-primary'
                : 'text-muted hover:bg-canvas hover:text-foreground',
            )
          }
        >
          <Icon className="h-[18px] w-[18px] shrink-0" strokeWidth={2} />
          {t(key)}
        </NavLink>
      ))}
      <NavLink
        to="/settings"
        onClick={onNavigate}
        className={({ isActive }) =>
          cn(
            'mt-2 flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition',
            isActive
              ? 'bg-primary-muted text-primary'
              : 'text-muted hover:bg-canvas hover:text-foreground',
          )
        }
      >
        <Settings className="h-[18px] w-[18px]" strokeWidth={2} />
        {t('nav.settings', 'Settings')}
      </NavLink>
    </nav>
  )
}

/**
 * Primary navigation sidebar for authenticated users (desktop).
 */
export default function Sidebar() {
  return (
    <aside className="fixed inset-y-0 left-0 z-30 hidden w-64 flex-col border-r border-border bg-surface lg:flex">
      <div className="flex h-16 items-center gap-2.5 border-b border-border px-5">
        <img src="/logo-mark.svg" alt="" className="h-8 w-8" />
        <span className="text-lg font-bold tracking-tight text-foreground">TaskPilot</span>
      </div>

      <div className="flex-1 overflow-y-auto p-3">
        <SidebarNav />
      </div>
    </aside>
  )
}

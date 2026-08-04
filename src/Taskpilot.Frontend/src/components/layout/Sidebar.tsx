import {
  BookOpen,
  Bookmark,
  Bot,
  Calendar,
  FolderKanban,
  LayoutDashboard,
  ListChecks,
  MessageSquare,
  NotebookPen,
  Search,
  Settings,
  Shield,
  ShoppingBag,
  MessagesSquare,
  Tag,
  Trophy,
  Users,
  type LucideIcon,
} from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { NavLink } from 'react-router-dom'
import { cn } from '../../lib/cn'
import { useAppSelector } from '../../store/hooks'
import BrandLogo from '../BrandLogo'
import { useBranding } from '../../hooks/useBranding'
import { useFeatures } from '../../hooks/useFeatures'

type NavItemDef = { to: string; key: string; icon: LucideIcon; end?: boolean }

/** A single nav row with a gradient accent bar when active. */
function NavItem({ item, onNavigate }: { item: NavItemDef; onNavigate?: () => void }) {
  const { t } = useTranslation()
  const Icon = item.icon
  return (
    <NavLink
      to={item.to}
      end={item.end}
      onClick={onNavigate}
      className={({ isActive }) =>
        cn(
          'group relative flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition',
          isActive ? 'bg-primary-muted text-primary font-semibold' : 'text-muted hover:bg-canvas hover:text-foreground',
        )
      }
    >
      {({ isActive }) => (
        <>
          <span
            className={cn(
              'gradient-primary absolute left-0 top-1/2 h-5 w-1 -translate-y-1/2 rounded-r-full transition-opacity',
              isActive ? 'opacity-100' : 'opacity-0',
            )}
          />
          <Icon className="h-[18px] w-[18px] shrink-0" strokeWidth={2} />
          {t(item.key)}
        </>
      )}
    </NavLink>
  )
}

/** Reusable sidebar navigation (desktop sidebar + mobile drawer), grouped into sections. */
export function SidebarNav({ onNavigate }: { onNavigate?: () => void }) {
  const { t } = useTranslation()
  const user = useAppSelector((s) => s.auth.user)
  const features = useFeatures()

  // Community section respects the admin's feature toggles.
  const community: NavItemDef[] = [
    ...(features.forumEnabled ? [{ to: '/forum', key: 'nav.forum', icon: MessagesSquare }] : []),
    ...(features.marketplaceEnabled ? [{ to: '/marketplace', key: 'nav.market', icon: ShoppingBag }] : []),
    { to: '/chat', key: 'nav.chat', icon: MessageSquare },
    { to: '/users', key: 'nav.users', icon: Users },
    { to: '/leaderboard', key: 'nav.leaderboard', icon: Trophy },
  ]

  const sections: { label: string | null; items: NavItemDef[] }[] = [
    { label: null, items: [{ to: '/', key: 'nav.dashboard', icon: LayoutDashboard, end: true }] },
    {
      label: 'nav.group.work',
      items: [
        { to: '/projects', key: 'nav.projects', icon: FolderKanban },
        { to: '/my-tasks', key: 'nav.myTasks', icon: ListChecks },
        { to: '/calendar', key: 'nav.calendar', icon: Calendar },
      ],
    },
    { label: 'nav.group.community', items: community },
    {
      label: 'nav.group.tools',
      items: [
        { to: '/assistant', key: 'nav.assistant', icon: Bot },
        { to: '/notes', key: 'nav.notes', icon: NotebookPen },
        { to: '/bookmarks', key: 'nav.bookmarks', icon: Bookmark },
        { to: '/search', key: 'nav.search', icon: Search },
      ],
    },
  ]

  return (
    <nav className="space-y-0.5">
      {sections.map((s) => (
        <div key={s.label ?? 'main'}>
          {s.label && (
            <p className="px-3 pb-1 pt-3 text-[10px] font-semibold uppercase tracking-wider text-muted/70">{t(s.label)}</p>
          )}
          {s.items.map((item) => (
            <NavItem key={item.to} item={item} onNavigate={onNavigate} />
          ))}
        </div>
      ))}

      {/* System: admin (admins only) + settings */}
      <div className="mt-2 space-y-0.5 border-t border-border pt-2">
        {user?.role === 'Admin' && <NavItem item={{ to: '/admin', key: 'nav.admin', icon: Shield }} onNavigate={onNavigate} />}
        <NavItem item={{ to: '/settings', key: 'nav.settings', icon: Settings }} onNavigate={onNavigate} />
      </div>

      {/* Public docs/help and pricing pages. */}
      <div className="mt-2 space-y-0.5 border-t border-border pt-2">
        <NavItem item={{ to: '/docs', key: 'docs.nav', icon: BookOpen }} onNavigate={onNavigate} />
        <NavItem item={{ to: '/pricing', key: 'pricing.nav', icon: Tag }} onNavigate={onNavigate} />
      </div>
    </nav>
  )
}

/**
 * Primary navigation sidebar for authenticated users (desktop).
 */
export default function Sidebar() {
  const { name } = useBranding()
  return (
    <aside className="fixed inset-y-0 left-0 z-30 hidden w-64 flex-col border-r border-border bg-surface lg:flex">
      <div className="flex h-16 items-center gap-2.5 border-b border-border px-5">
        <BrandLogo className="h-8 w-8 rounded" />
        <span className="text-lg font-bold tracking-tight text-foreground">{name}</span>
      </div>

      <div className="flex-1 overflow-y-auto p-3">
        <SidebarNav />
      </div>
    </aside>
  )
}

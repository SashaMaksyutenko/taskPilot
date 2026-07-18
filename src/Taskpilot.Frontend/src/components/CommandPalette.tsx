import { useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { AnimatePresence, motion } from 'framer-motion'
import {
  Calendar,
  LayoutDashboard,
  FileText,
  FolderKanban,
  LogOut,
  MessageSquare,
  Moon,
  Bookmark,
  Search,
  Settings,
  ShieldCheck,
  ShoppingBag,
  StickyNote,
  Bot,
  Sparkles,
  type LucideIcon,
} from 'lucide-react'
import { useAppDispatch } from '../store/hooks'
import { useFeatures } from '../hooks/useFeatures'
import { logout } from '../store/authSlice'
import { searchService, type SearchResults } from '../services/searchService'
import { chatbotService } from '../services/chatbotService'

/** One selectable row in the palette. */
interface PaletteItem {
  id: string
  section: string
  label: string
  sublabel?: string | null
  icon: LucideIcon
  run: () => void
}

const empty: SearchResults = { projects: [], tasks: [], topics: [], users: [] }

/** Static navigation targets — [route, i18n label key, icon]. */
const NAV: [string, string, LucideIcon][] = [
  ['/', 'nav.dashboard', LayoutDashboard],
  ['/projects', 'nav.projects', FolderKanban],
  ['/calendar', 'nav.calendar', Calendar],
  ['/forum', 'nav.forum', MessageSquare],
  ['/marketplace', 'nav.market', ShoppingBag],
  ['/chat', 'nav.chat', MessageSquare],
  ['/assistant', 'nav.assistant', Bot],
  ['/notes', 'nav.notes', StickyNote],
  ['/bookmarks', 'nav.bookmarks', Bookmark],
  ['/search', 'nav.search', Search],
  ['/admin', 'nav.admin', ShieldCheck],
  ['/settings', 'nav.settings', Settings],
]

/**
 * Cmd/Ctrl+K command palette: jump to any page or search across projects, tasks, forum
 * and people, plus a couple of quick actions. Fully keyboard-driven.
 */
export default function CommandPalette({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const dispatch = useAppDispatch()
  const features = useFeatures()

  const [query, setQuery] = useState('')
  const [results, setResults] = useState<SearchResults>(empty)
  const [active, setActive] = useState(0)
  const [aiEnabled, setAiEnabled] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)
  const listRef = useRef<HTMLDivElement>(null)

  // Whether to offer "Ask AI"; fetched once for the session.
  useEffect(() => {
    chatbotService.status().then((s) => setAiEnabled(s.enabled)).catch(() => setAiEnabled(false))
  }, [])

  // Reset and focus whenever the palette opens.
  useEffect(() => {
    if (!open) return
    setQuery('')
    setResults(empty)
    setActive(0)
    // Focus after the open animation starts.
    const id = setTimeout(() => inputRef.current?.focus(), 30)
    return () => clearTimeout(id)
  }, [open])

  // Debounced global search once the query is long enough.
  useEffect(() => {
    const term = query.trim()
    if (term.length < 2) {
      setResults(empty)
      return
    }
    const handle = setTimeout(() => {
      searchService.search(term).then(setResults).catch(() => setResults(empty))
    }, 200)
    return () => clearTimeout(handle)
  }, [query])

  // A quick action closes the palette after running.
  const act = (fn: () => void) => () => {
    onClose()
    fn()
  }

  // Build the flat, ordered item list the arrow keys move through.
  const items = useMemo<PaletteItem[]>(() => {
    const q = query.trim().toLowerCase()

    const nav: PaletteItem[] = NAV
      // Drop the entry point for any feature the admin has switched off.
      .filter(([to]) =>
        (to !== '/forum' || features.forumEnabled) &&
        (to !== '/marketplace' || features.marketplaceEnabled),
      )
      .filter(([, key]) => !q || t(key).toLowerCase().includes(q))
      .map(([to, key, icon]) => ({
        id: 'nav:' + to,
        section: t('cmd.navigation'),
        label: t(key),
        icon,
        run: act(() => navigate(to)),
      }),
    )

    const group = (
      section: string,
      list: SearchResults['projects'],
      icon: LucideIcon,
      route: (id: string) => string,
    ): PaletteItem[] =>
      list.map((i) => ({
        id: section + ':' + i.id + i.label,
        section,
        label: i.label,
        sublabel: i.sublabel,
        icon,
        run: act(() => navigate(route(i.id))),
      }))

    const actions: PaletteItem[] = [
      {
        id: 'action:theme',
        section: t('cmd.actions'),
        label: t('topbar.theme'),
        icon: Moon,
        run: act(() => {
          const next = !document.documentElement.classList.contains('dark')
          document.documentElement.classList.toggle('dark', next)
          localStorage.setItem('theme', next ? 'dark' : 'light')
        }),
      },
      {
        id: 'action:logout',
        section: t('cmd.actions'),
        label: t('nav.logout'),
        icon: LogOut,
        run: act(() => dispatch(logout())),
      },
    ].filter((a) => !q || a.label.toLowerCase().includes(q))

    // "Ask AI" runs the typed text as a question — opens the assistant which auto-sends it.
    // Exempt from the label filter since the item is inherently about the current query.
    const term = query.trim()
    const aiAction: PaletteItem[] =
      aiEnabled && term.length >= 2
        ? [
            {
              id: 'action:ask-ai',
              section: t('cmd.actions'),
              label: t('cmd.askAi', { query: term }),
              icon: Sparkles,
              run: act(() => navigate('/assistant', { state: { prompt: term } })),
            },
          ]
        : []

    return [
      ...nav,
      ...group(t('search.projects'), results.projects, FolderKanban, (id) => `/projects/${id}`),
      ...group(t('search.tasks'), results.tasks, FileText, (id) => `/projects/${id}`),
      ...group(t('search.topics'), results.topics, MessageSquare, (id) => `/forum/${id}`),
      ...group(t('search.users'), results.users, Search, (id) => `/users/${id}`),
      ...aiAction,
      ...actions,
    ]
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query, results, t, aiEnabled, features])

  // Keep the active index in range as the list shrinks/grows.
  useEffect(() => {
    setActive((a) => Math.min(a, Math.max(0, items.length - 1)))
  }, [items.length])

  const onKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setActive((a) => Math.min(a + 1, items.length - 1))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setActive((a) => Math.max(a - 1, 0))
    } else if (e.key === 'Enter') {
      e.preventDefault()
      items[active]?.run()
    } else if (e.key === 'Escape') {
      e.preventDefault()
      onClose()
    }
  }

  // Scroll the active row into view.
  useEffect(() => {
    listRef.current?.querySelector<HTMLElement>(`[data-index="${active}"]`)?.scrollIntoView({ block: 'nearest' })
  }, [active])

  // Render items grouped by their section header while keeping a global index.
  let index = -1

  return (
    <AnimatePresence>
      {open && (
        <motion.div
          className="fixed inset-0 z-[70] flex items-start justify-center bg-black/40 p-4 pt-[12vh]"
          onClick={onClose}
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.12 }}
        >
          <motion.div
            className="flex w-full max-w-xl flex-col overflow-hidden rounded-xl border border-border bg-surface shadow-elevated"
            onClick={(e) => e.stopPropagation()}
            initial={{ scale: 0.98, y: 8 }}
            animate={{ scale: 1, y: 0 }}
            exit={{ scale: 0.98, y: 8 }}
            transition={{ duration: 0.12 }}
          >
            <div className="flex items-center gap-2 border-b border-border px-4">
              <Search className="h-4 w-4 flex-none text-muted" />
              <input
                ref={inputRef}
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                onKeyDown={onKeyDown}
                placeholder={t('cmd.placeholder')}
                className="w-full bg-transparent py-3.5 text-sm outline-none placeholder:text-muted"
              />
            </div>

            <div ref={listRef} className="max-h-[50vh] overflow-y-auto p-2">
              {items.length === 0 ? (
                <p className="px-3 py-6 text-center text-sm text-muted">{t('cmd.noResults')}</p>
              ) : (
                items.map((item, i) => {
                  index++
                  const showHeader = i === 0 || items[i - 1].section !== item.section
                  const Icon = item.icon
                  const isActive = index === active
                  const myIndex = index
                  return (
                    <div key={item.id}>
                      {showHeader && (
                        <p className="px-3 pb-1 pt-2 text-[11px] font-semibold uppercase tracking-wide text-muted">
                          {item.section}
                        </p>
                      )}
                      <button
                        data-index={myIndex}
                        onMouseEnter={() => setActive(myIndex)}
                        onClick={item.run}
                        className={`flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left text-sm ${
                          isActive ? 'bg-primary/10 text-foreground' : 'text-foreground/90 hover:bg-canvas'
                        }`}
                      >
                        <Icon className={`h-4 w-4 flex-none ${isActive ? 'text-primary' : 'text-muted'}`} />
                        <span className="min-w-0 flex-1 truncate">{item.label}</span>
                        {item.sublabel && <span className="flex-none text-xs text-muted">{item.sublabel}</span>}
                      </button>
                    </div>
                  )
                })
              )}
            </div>

            <div className="flex items-center gap-3 border-t border-border px-4 py-2 text-[11px] text-muted">
              <span>↑↓ {t('cmd.navigate')}</span>
              <span>↵ {t('cmd.select')}</span>
              <span>esc {t('cmd.close')}</span>
            </div>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  )
}

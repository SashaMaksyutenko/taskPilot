import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import Avatar from '../components/Avatar'
import EmptyState from '../components/feedback/EmptyState'
import {
  searchService,
  type SavedSearch,
  type SearchItem,
  type SearchResults,
} from '../services/searchService'
import { semanticSearchService, type SemanticResult, type SemanticStatus } from '../services/semanticSearchService'

const empty: SearchResults = { projects: [], tasks: [], topics: [], users: [] }

/** A group of search hits with a heading and per-item links. */
function Group({
  title,
  items,
  linkFor,
  withAvatar = false,
}: {
  title: string
  items: SearchItem[]
  linkFor: (i: SearchItem) => string
  withAvatar?: boolean
}) {
  if (items.length === 0) return null
  return (
    <div className="rounded-[var(--radius-card)] border border-border bg-surface p-4">
      <h2 className="mb-2 text-sm font-bold uppercase tracking-wide text-muted">{title}</h2>
      <ul className="divide-y divide-border">
        {items.map((i) => (
          <li key={`${i.id}-${i.label}`}>
            <Link to={linkFor(i)} className="flex items-center gap-2 py-2 text-sm hover:opacity-80">
              {withAvatar && <Avatar name={i.label} src={i.avatarUrl} size={26} />}
              <span className="font-medium">{i.label}</span>
              {i.sublabel && <span className="text-xs text-muted">· {i.sublabel}</span>}
            </Link>
          </li>
        ))}
      </ul>
    </div>
  )
}

/** One semantic hit: source badge, title, snippet and a match-percentage. */
function SemanticRow({ r }: { r: SemanticResult }) {
  const { t } = useTranslation()
  return (
    <Link to={r.url} className="block rounded-[var(--radius-card)] border border-border bg-surface p-3 transition hover:bg-canvas">
      <div className="flex items-center gap-2">
        <span className="flex-none rounded-full bg-primary-muted px-2 py-0.5 text-[10px] font-semibold text-primary">
          {t(`search.source.${r.sourceType}`, r.sourceType)}
        </span>
        <span className="min-w-0 flex-1 truncate font-medium">{r.title}</span>
        <span className="flex-none text-xs font-semibold text-muted tabular-nums">{Math.round(r.score * 100)}%</span>
      </div>
      {r.snippet && <p className="mt-1 truncate text-xs text-muted">{r.snippet}</p>}
    </Link>
  )
}

/**
 * Global search page. Two modes: keyword search (projects/tasks/topics/users, exact-ish) and
 * semantic search (meaning-based over the user's tasks & notes; shown only when embeddings are
 * configured on the server).
 */
export default function SearchPage() {
  const { t } = useTranslation()
  const [mode, setMode] = useState<'keyword' | 'semantic'>('keyword')
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<SearchResults>(empty)
  const [saved, setSaved] = useState<SavedSearch[]>([])

  const [sem, setSem] = useState<SemanticStatus | null>(null)
  const [semResults, setSemResults] = useState<SemanticResult[]>([])
  const [reindexing, setReindexing] = useState(false)

  // Keyword search: debounced, only in keyword mode.
  useEffect(() => {
    if (mode !== 'keyword') return
    const term = query.trim()
    if (term.length < 2) {
      setResults(empty)
      return
    }
    const handle = setTimeout(() => {
      searchService.search(term).then(setResults).catch(() => setResults(empty))
    }, 300)
    return () => clearTimeout(handle)
  }, [query, mode])

  // Semantic search: debounced, only in semantic mode when enabled.
  useEffect(() => {
    if (mode !== 'semantic' || !sem?.enabled) return
    const term = query.trim()
    if (term.length < 2) {
      setSemResults([])
      return
    }
    const handle = setTimeout(() => {
      semanticSearchService.search(term).then((r) => setSemResults(r.results)).catch(() => setSemResults([]))
    }, 350)
    return () => clearTimeout(handle)
  }, [query, mode, sem?.enabled])

  // Load saved searches + semantic status once.
  useEffect(() => {
    searchService.getSaved().then(setSaved).catch(() => {})
    semanticSearchService.status().then(setSem).catch(() => setSem({ enabled: false, indexedCount: 0 }))
  }, [])

  const reindex = async () => {
    setReindexing(true)
    try {
      const res = await semanticSearchService.reindex()
      setSem({ enabled: res.enabled, indexedCount: res.indexed })
    } catch {
      /* leave status as-is */
    } finally {
      setReindexing(false)
    }
  }

  const saveCurrent = async () => {
    const term = query.trim()
    if (term.length < 2) return
    const created = await searchService.saveSearch(term, term).catch(() => null)
    if (created) setSaved((prev) => [created, ...prev.filter((s) => s.id !== created.id)])
  }

  const removeSaved = async (id: string) => {
    await searchService.deleteSaved(id).catch(() => {})
    setSaved((prev) => prev.filter((s) => s.id !== id))
  }

  const alreadySaved = saved.some((s) => s.query === query.trim())
  const total = results.projects.length + results.tasks.length + results.topics.length + results.users.length
  const tabCls = (active: boolean) =>
    `px-3 py-1.5 text-sm font-medium transition ${active ? 'bg-primary text-white' : 'text-foreground hover:bg-canvas'}`

  return (
    <div className="mx-auto max-w-3xl px-6 py-8">
      <div className="mb-4 flex items-center justify-between gap-3">
        <h1 className="text-2xl font-bold">{t('search.title')}</h1>
        {/* Mode switch — Semantic only appears when the server has embeddings configured. */}
        <div className="inline-flex overflow-hidden rounded-lg border border-border">
          <button onClick={() => setMode('keyword')} className={tabCls(mode === 'keyword')}>{t('search.mode.keyword')}</button>
          <button onClick={() => setMode('semantic')} className={tabCls(mode === 'semantic')}>{t('search.mode.semantic')}</button>
        </div>
      </div>

      <div className="mb-4 flex gap-2">
        <input
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder={mode === 'semantic' ? t('search.semantic.placeholder') : t('search.placeholder')}
          autoFocus
          className="w-full rounded-lg border border-border bg-canvas px-4 py-2.5 outline-none focus:border-primary"
        />
        {mode === 'keyword' ? (
          <button
            onClick={saveCurrent}
            disabled={query.trim().length < 2 || alreadySaved}
            className="flex-none rounded-lg bg-primary px-4 py-2.5 text-sm font-semibold text-white hover:bg-primary-hover disabled:opacity-50"
          >
            {t('search.save')}
          </button>
        ) : (
          sem?.enabled && (
            <button
              onClick={reindex}
              disabled={reindexing}
              className="flex-none rounded-lg border border-border px-4 py-2.5 text-sm font-semibold hover:bg-canvas disabled:opacity-50"
              title={t('search.semantic.indexed', { count: sem.indexedCount })}
            >
              {reindexing ? t('search.semantic.reindexing') : t('search.semantic.reindex')}
            </button>
          )
        )}
      </div>

      {/* Saved searches (keyword mode) */}
      {mode === 'keyword' && saved.length > 0 && (
        <div className="mb-6 flex flex-wrap gap-2">
          {saved.map((s) => (
            <span key={s.id} className="flex items-center gap-1 rounded-full border border-border bg-surface px-3 py-1 text-sm">
              <button onClick={() => setQuery(s.query)} className="font-medium hover:text-primary" title={s.query}>
                {s.name}
              </button>
              <button onClick={() => removeSaved(s.id)} className="text-muted hover:text-red-600" title={t('search.removeSaved')}>
                ✕
              </button>
            </span>
          ))}
        </div>
      )}

      {mode === 'keyword' ? (
        query.trim().length < 2 ? (
          <p className="text-muted">{t('search.hint')}</p>
        ) : total === 0 ? (
          <EmptyState message={t('search.noResults')} />
        ) : (
          <div className="space-y-4">
            <Group title={t('search.projects')} items={results.projects} linkFor={(i) => `/projects/${i.id}`} />
            <Group title={t('search.tasks')} items={results.tasks} linkFor={(i) => `/projects/${i.id}`} />
            <Group title={t('search.topics')} items={results.topics} linkFor={(i) => `/forum/${i.id}`} />
            <Group title={t('search.users')} items={results.users} linkFor={(i) => `/users/${i.id}`} withAvatar />
          </div>
        )
      ) : sem && !sem.enabled ? (
        // Embeddings not configured — explain how to turn it on.
        <div className="rounded-[var(--radius-card)] border border-border bg-surface p-5 text-sm">
          <p className="mb-1 font-semibold">{t('search.semantic.disabledTitle')}</p>
          <p className="text-muted">{t('search.semantic.disabledHint')}</p>
        </div>
      ) : query.trim().length < 2 ? (
        <p className="text-muted">{t('search.semantic.hint')}</p>
      ) : semResults.length === 0 ? (
        <EmptyState message={t('search.semantic.noResults')} />
      ) : (
        <div className="space-y-2">
          {semResults.map((r) => (
            <SemanticRow key={`${r.sourceType}-${r.sourceId}`} r={r} />
          ))}
        </div>
      )}
    </div>
  )
}

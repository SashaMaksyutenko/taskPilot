import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import Avatar from '../components/Avatar'
import EmptyState from '../components/EmptyState'
import { searchService, type SearchItem, type SearchResults } from '../services/searchService'

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
    <div className="rounded-xl border border-border bg-surface p-4">
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

/**
 * Global search page: queries projects, tasks, forum topics and users, grouped.
 */
export default function SearchPage() {
  const { t } = useTranslation()
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<SearchResults>(empty)

  // Debounced search a short moment after typing stops.
  useEffect(() => {
    const term = query.trim()
    if (term.length < 2) {
      setResults(empty)
      return
    }
    const handle = setTimeout(() => {
      searchService.search(term).then(setResults).catch(() => setResults(empty))
    }, 300)
    return () => clearTimeout(handle)
  }, [query])

  const total = results.projects.length + results.tasks.length + results.topics.length + results.users.length

  return (
    <div className="mx-auto max-w-3xl px-6 py-8">
        <h1 className="mb-4 text-2xl font-bold">{t('search.title')}</h1>

        <input
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder={t('search.placeholder')}
          autoFocus
          className="mb-6 w-full rounded-lg border border-border bg-canvas px-4 py-2.5 outline-none focus:border-primary"
        />

        {query.trim().length < 2 ? (
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
        )}
      </div>
  )
}

import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import Avatar from '../components/Avatar'
import Button from '../components/ui/Button'
import Card from '../components/ui/Card'
import Input from '../components/ui/Input'
import { SkeletonCard } from '../components/ui/Skeleton'
import { userService, type UserDirectoryItem } from '../services/userService'

const PAGE_SIZE = 24

/**
 * Public members directory: a searchable, paged grid of users. Each card links to that
 * user's public profile.
 */
export default function UsersPage() {
  const { t } = useTranslation()
  const [items, setItems] = useState<UserDirectoryItem[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  // Debounced load whenever the page or the search term changes.
  useEffect(() => {
    const id = setTimeout(() => {
      setLoading(true)
      userService
        .getDirectory({ page, pageSize: PAGE_SIZE, search: search.trim() || undefined })
        .then((r) => {
          setItems(r.items)
          setTotal(r.total)
        })
        .catch(() => {})
        .finally(() => setLoading(false))
    }, 250)
    return () => clearTimeout(id)
  }, [page, search])

  return (
    <div className="mx-auto max-w-5xl">
      <div className="mb-6 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <h1 className="page-title">{t('users.title')}</h1>
        <Input
          value={search}
          onChange={(e) => {
            setSearch(e.target.value)
            setPage(1)
          }}
          placeholder={t('users.searchPlaceholder')}
          className="sm:w-72"
        />
      </div>

      {loading && items.length === 0 ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <SkeletonCard key={i} />
          ))}
        </div>
      ) : items.length === 0 ? (
        <p className="py-10 text-center text-sm text-muted">{t('users.empty')}</p>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {items.map((u) => (
            <Link key={u.id} to={`/users/${u.id}`} className="block">
              <Card hover className="flex items-center gap-3 p-4">
                <Avatar name={u.name} src={u.avatarUrl} size={40} />
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2">
                    <span className="truncate font-semibold">{u.name}</span>
                    <span className="flex-none rounded-full border border-border px-2 py-0.5 text-[10px] font-semibold text-muted">
                      {t(`admin.roles.${u.role}`, u.role)}
                    </span>
                  </div>
                  {u.title && <p className="truncate text-sm text-muted">{u.title}</p>}
                  {u.location && <p className="truncate text-xs text-muted">📍 {u.location}</p>}
                </div>
              </Card>
            </Link>
          ))}
        </div>
      )}

      {totalPages > 1 && (
        <div className="mt-6 flex items-center justify-center gap-4 text-sm">
          <Button variant="secondary" size="sm" onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1}>
            {t('audit.prev')}
          </Button>
          <span className="text-muted">{t('audit.pageOf', { page, total: totalPages })}</span>
          <Button
            variant="secondary"
            size="sm"
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            disabled={page >= totalPages}
          >
            {t('audit.next')}
          </Button>
        </div>
      )}
    </div>
  )
}

import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Bookmark as BookmarkIcon, FolderKanban, MessagesSquare, MessageSquare, X } from 'lucide-react'
import EmptyState from '../components/EmptyState'
import Card from '../components/ui/Card'
import { SkeletonCard } from '../components/ui/Skeleton'
import { bookmarkService, type Bookmark, type BookmarkType } from '../services/bookmarkService'

// Icon per bookmark type.
const TYPE_ICON: Record<BookmarkType, typeof FolderKanban> = {
  Task: FolderKanban,
  Topic: MessagesSquare,
  Message: MessageSquare,
}

/** Quick-access list of the user's saved bookmarks (tasks, topics, messages). */
export default function BookmarksPage() {
  const { t } = useTranslation()
  const [items, setItems] = useState<Bookmark[]>([])
  const [loading, setLoading] = useState(true)

  const load = () => {
    setLoading(true)
    bookmarkService
      .getMine()
      .then(setItems)
      .catch(() => {})
      .finally(() => setLoading(false))
  }
  useEffect(load, [])

  const remove = async (id: string) => {
    await bookmarkService.remove(id).catch(() => {})
    setItems((prev) => prev.filter((b) => b.id !== id))
  }

  return (
    <div className="mx-auto max-w-3xl">
      <h1 className="mb-6 flex items-center gap-2 text-2xl font-bold">
        <BookmarkIcon className="h-6 w-6 text-primary" />
        {t('bookmarks.title')}
      </h1>

      {loading && items.length === 0 ? (
        <div className="space-y-2">
          {Array.from({ length: 4 }).map((_, i) => (
            <SkeletonCard key={i} />
          ))}
        </div>
      ) : items.length === 0 ? (
        <EmptyState message={t('bookmarks.empty')} />
      ) : (
        <ul className="space-y-2">
          {items.map((b) => {
            const Icon = TYPE_ICON[b.type] ?? BookmarkIcon
            return (
              <li key={b.id}>
                <Card hover className="flex items-center gap-3 p-4">
                  <span className="flex-none rounded-lg bg-primary/10 p-2 text-primary">
                    <Icon className="h-5 w-5" />
                  </span>
                  <Link to={b.link} className="min-w-0 flex-1">
                    <div className="truncate font-semibold">{b.title || t(`bookmarks.type.${b.type}`)}</div>
                    <div className="text-xs text-muted">
                      {t(`bookmarks.type.${b.type}`)} · {new Date(b.createdAt).toLocaleDateString()}
                    </div>
                  </Link>
                  <button
                    onClick={() => remove(b.id)}
                    className="flex-none rounded-lg p-2 text-muted transition hover:bg-canvas hover:text-red-600"
                    aria-label={t('bookmarks.remove')}
                    title={t('bookmarks.remove')}
                  >
                    <X className="h-4 w-4" />
                  </button>
                </Card>
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}

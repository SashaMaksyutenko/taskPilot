import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import Avatar from '../components/Avatar'
import MarkdownEditor from '../components/MarkdownEditor'
import EmptyState from '../components/EmptyState'
import TagInput from '../components/TagInput'
import TopicContextMenu from '../components/TopicContextMenu'
import Button from '../components/ui/Button'
import Card from '../components/ui/Card'
import Input from '../components/ui/Input'
import Select from '../components/ui/Select'
import { SkeletonCard } from '../components/ui/Skeleton'
import { apiErrorMessage } from '../lib/apiError'
import { forumService } from '../services/forumService'
import { bookmarkService } from '../services/bookmarkService'
import { notify } from '../lib/toast'
import { useAppSelector } from '../store/hooks'
import type { TopicListItem } from '../types/forum'

/**
 * Forum home: list of discussion topics and a form to start a new one.
 */
export default function ForumPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const currentUser = useAppSelector((s) => s.auth.user)
  const isAdmin = currentUser?.role === 'Admin'
  const PAGE_SIZE = 10
  const [topics, setTopics] = useState<TopicListItem[]>([])
  const [page, setPage] = useState(1)
  const [total, setTotal] = useState(0)
  const [title, setTitle] = useState('')
  const [body, setBody] = useState('')
  const [tags, setTags] = useState<string[]>([])
  const [loading, setLoading] = useState(false)
  const [listLoading, setListLoading] = useState(true)
  const [error, setError] = useState('')
  const [search, setSearch] = useState('')
  const [sort, setSort] = useState<'latest' | 'active' | 'top'>('latest')
  const [solvedFilter, setSolvedFilter] = useState<'all' | 'solved' | 'unsolved'>('all')
  const [searchParams] = useSearchParams()
  const [tagFilter, setTagFilter] = useState<string | null>(searchParams.get('tag'))

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  const load = (p: number) => {
    setListLoading(true)
    forumService
      .getTopics({
        page: p,
        pageSize: PAGE_SIZE,
        search: search.trim() || undefined,
        sort,
        solved: solvedFilter === 'all' ? undefined : solvedFilter === 'solved',
        tag: tagFilter || undefined,
      })
      .then((r) => {
        setTopics(r.items)
        setTotal(r.total)
      })
      .catch(() => {})
      .finally(() => setListLoading(false))
  }

  useEffect(() => {
    const id = setTimeout(() => load(page), 250)
    return () => clearTimeout(id)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, search, sort, solvedFilter, tagFilter])

  const create = async () => {
    if (!title.trim() || !body.trim()) return
    setLoading(true)
    setError('')
    try {
      await forumService.createTopic({ title: title.trim(), body: body.trim(), tags })
      setTitle('')
      setBody('')
      setTags([])
      if (page === 1) load(1)
      else setPage(1)
    } catch (e) {
      setError(apiErrorMessage(e))
    } finally {
      setLoading(false)
    }
  }

  const removeTopic = async (id: string) => {
    await forumService.deleteTopic(id).catch(() => {})
    load(page)
  }

  // Which topics the current user has bookmarked (for the context-menu label).
  const [bookmarkedIds, setBookmarkedIds] = useState<Set<string>>(new Set())
  useEffect(() => {
    bookmarkService
      .getMine()
      .then((bs) => setBookmarkedIds(new Set(bs.filter((b) => b.type === 'Topic').map((b) => b.entityId))))
      .catch(() => {})
  }, [])

  const toggleBookmark = async (topic: TopicListItem) => {
    const now = await bookmarkService
      .toggle({ type: 'Topic', entityId: topic.id, title: topic.title, link: `/forum/${topic.id}` })
      .catch(() => null)
    if (now === null) return
    setBookmarkedIds((prev) => {
      const next = new Set(prev)
      if (now) next.add(topic.id)
      else next.delete(topic.id)
      return next
    })
    notify.success(now ? t('bookmarks.added') : t('bookmarks.removed'))
  }

  const togglePin = async (topic: TopicListItem) => {
    await forumService.setPinned(topic.id, !topic.isPinned).catch(() => {})
    load(page)
  }

  const toggleLock = async (topic: TopicListItem) => {
    await forumService.setLocked(topic.id, !topic.isLocked).catch(() => {})
    load(page)
  }

  const changeSort = (value: 'latest' | 'active' | 'top') => {
    setSort(value)
    setPage(1)
  }
  const changeSolved = (value: 'all' | 'solved' | 'unsolved') => {
    setSolvedFilter(value)
    setPage(1)
  }
  const changeSearch = (value: string) => {
    setSearch(value)
    setPage(1)
  }
  const changeTag = (value: string | null) => {
    setTagFilter(value)
    setPage(1)
  }

  return (
    <div className="mx-auto max-w-3xl">
      <h1 className="page-title mb-6">{t('forum.title')}</h1>

      <Card className="mb-8 p-5">
        <h2 className="mb-3 font-bold">{t('forum.startDiscussion')}</h2>
        <Input
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder={t('forum.topicTitle')}
          className="mb-3"
        />
        <div className="mb-3">
          <MarkdownEditor
            value={body}
            onChange={setBody}
            placeholder={t('forum.bodyPlaceholder')}
            className="w-full rounded-lg border border-border bg-surface px-3 py-2 outline-none focus:border-primary"
          />
        </div>
        <div className="mb-4">
          <TagInput tags={tags} onChange={setTags} />
        </div>
        <div className="flex items-center gap-3">
          <Button onClick={create} disabled={loading}>
            {t('forum.postTopic')}
          </Button>
          {error && <span className="text-sm font-medium text-red-600">{error}</span>}
        </div>
      </Card>

      <div className="mb-4 flex flex-wrap items-center gap-2">
        <Input
          value={search}
          onChange={(e) => changeSearch(e.target.value)}
          placeholder={t('forum.searchPlaceholder')}
          className="min-w-0 flex-1"
        />
        <Select value={sort} onChange={(e) => changeSort(e.target.value as 'latest' | 'active' | 'top')}>
          <option value="latest">{t('forum.sort.latest')}</option>
          <option value="active">{t('forum.sort.active')}</option>
          <option value="top">{t('forum.sort.top')}</option>
        </Select>
        <Select
          value={solvedFilter}
          onChange={(e) => changeSolved(e.target.value as 'all' | 'solved' | 'unsolved')}
        >
          <option value="all">{t('forum.filter.all')}</option>
          <option value="solved">{t('forum.filter.solved')}</option>
          <option value="unsolved">{t('forum.filter.unsolved')}</option>
        </Select>
      </div>

      {tagFilter && (
        <div className="mb-4 flex items-center gap-2 text-sm">
          <span className="text-muted">{t('forum.filteredByTag')}</span>
          <span className="flex items-center gap-1 rounded-full bg-primary/10 px-2 py-0.5 text-xs font-medium text-primary">
            #{tagFilter}
            <button type="button" onClick={() => changeTag(null)} className="text-muted hover:text-red-600">
              ✕
            </button>
          </span>
        </div>
      )}

      {listLoading && topics.length === 0 ? (
        <div className="space-y-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <SkeletonCard key={i} />
          ))}
        </div>
      ) : topics.length === 0 ? (
        <EmptyState message={t('forum.empty')} />
      ) : (
        <ul className="space-y-2">
          {topics.map((topic) => (
            <li key={topic.id}>
              <TopicContextMenu
                topicId={topic.id}
                canDelete={currentUser?.id === topic.authorId || currentUser?.role === 'Admin'}
                onDelete={() => removeTopic(topic.id)}
                isPinned={topic.isPinned}
                canPin={isAdmin}
                onTogglePin={() => togglePin(topic)}
                isLocked={topic.isLocked}
                canLock={isAdmin || currentUser?.id === topic.authorId}
                onToggleLock={() => toggleLock(topic)}
                bookmarked={bookmarkedIds.has(topic.id)}
                onBookmark={() => toggleBookmark(topic)}
              >
                <Link to={`/forum/${topic.id}`} className="block">
                  <Card hover className="flex items-center gap-3 p-4">
                    <Avatar name={topic.authorName} src={topic.authorAvatarUrl} size={38} />
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-1.5 truncate font-semibold">
                        {topic.isPinned && <span>📌</span>}
                        {topic.isLocked && <span title={t('topic.locked')}>🔒</span>}
                        <span className="truncate">{topic.title}</span>
                        {topic.isSolved && (
                          <span className="flex-none rounded-full bg-emerald-100 px-2 py-0.5 text-[10px] font-semibold text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300">
                            ✓ {t('forum.solved')}
                          </span>
                        )}
                      </div>
                      <div className="text-xs text-muted">
                        {t('forum.by')}{' '}
                        <span
                          onClick={(e) => {
                            e.preventDefault()
                            e.stopPropagation()
                            navigate(`/users/${topic.authorId}`)
                          }}
                          className="cursor-pointer font-medium text-foreground hover:underline"
                        >
                          {topic.authorName}
                        </span>{' '}
                        · {new Date(topic.createdAt).toLocaleDateString()}
                      </div>
                      {topic.tags.length > 0 && (
                        <div className="mt-1.5 flex flex-wrap gap-1">
                          {topic.tags.map((tag) => (
                            <button
                              key={tag}
                              type="button"
                              onClick={(e) => {
                                e.preventDefault()
                                e.stopPropagation()
                                changeTag(tag)
                              }}
                              className="rounded-full bg-primary/10 px-2 py-0.5 text-[10px] font-medium text-primary hover:bg-primary/20"
                            >
                              #{tag}
                            </button>
                          ))}
                        </div>
                      )}
                    </div>
                    <div className="flex flex-none gap-4 text-center text-xs text-muted">
                      <div>
                        <div className="font-bold text-foreground">{topic.replyCount}</div>
                        {t('forum.replies')}
                      </div>
                      <div>
                        <div className="font-bold text-foreground">{topic.viewCount}</div>
                        {t('forum.views')}
                      </div>
                    </div>
                  </Card>
                </Link>
              </TopicContextMenu>
            </li>
          ))}
        </ul>
      )}

      {totalPages > 1 && (
        <div className="mt-6 flex items-center justify-center gap-4 text-sm">
          <Button
            variant="secondary"
            size="sm"
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={page <= 1}
          >
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

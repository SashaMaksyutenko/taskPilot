import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import Navbar from '../components/Navbar'
import TopicContextMenu from '../components/TopicContextMenu'
import { forumService } from '../services/forumService'
import { useAppSelector } from '../store/hooks'
import type { TopicListItem } from '../types/forum'

/**
 * Forum home: list of discussion topics and a form to start a new one.
 */
export default function ForumPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const currentUser = useAppSelector((s) => s.auth.user)
  const [topics, setTopics] = useState<TopicListItem[]>([])
  const [title, setTitle] = useState('')
  const [body, setBody] = useState('')
  const [loading, setLoading] = useState(false)

  const load = () => {
    forumService.getTopics().then(setTopics).catch(() => {})
  }

  useEffect(load, [])

  const create = async () => {
    if (!title.trim() || !body.trim()) return
    setLoading(true)
    try {
      await forumService.createTopic({ title: title.trim(), body: body.trim() })
      setTitle('')
      setBody('')
      load()
    } finally {
      setLoading(false)
    }
  }

  const removeTopic = async (id: string) => {
    await forumService.deleteTopic(id).catch(() => {})
    setTopics((prev) => prev.filter((x) => x.id !== id))
  }

  return (
    <div className="min-h-screen bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      <Navbar />
      <main className="mx-auto max-w-3xl px-6 py-8">
        <h1 className="mb-6 text-2xl font-bold">{t('forum.title')}</h1>

        {/* New topic */}
        <div className="mb-8 rounded-xl border border-slate-200 bg-white p-5 dark:border-slate-700 dark:bg-slate-800">
          <h2 className="mb-3 font-bold">{t('forum.startDiscussion')}</h2>
          <input
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder={t('forum.topicTitle')}
            className="mb-2 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
          />
          <textarea
            value={body}
            onChange={(e) => setBody(e.target.value)}
            placeholder={t('forum.bodyPlaceholder')}
            rows={3}
            className="mb-3 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
          />
          <button
            onClick={create}
            disabled={loading}
            className="rounded-lg bg-[#1E2A44] px-5 py-2 font-semibold text-white transition hover:bg-[#27345a] disabled:opacity-60"
          >
            {t('forum.postTopic')}
          </button>
        </div>

        {/* Topic list */}
        {topics.length === 0 ? (
          <p className="text-slate-400">{t('forum.empty')}</p>
        ) : (
          <ul className="space-y-2">
            {topics.map((topic) => (
              <li key={topic.id}>
                <TopicContextMenu
                  topicId={topic.id}
                  canDelete={currentUser?.id === topic.authorId || currentUser?.role === 'Admin'}
                  onDelete={() => removeTopic(topic.id)}
                >
                <Link
                  to={`/forum/${topic.id}`}
                  className="flex items-center gap-3 rounded-xl border border-slate-200 bg-white p-4 transition hover:shadow-sm dark:border-slate-700 dark:bg-slate-800"
                >
                  <div className="min-w-0 flex-1">
                    <div className="truncate font-semibold">
                      {topic.isPinned && <span className="mr-1">📌</span>}
                      {topic.title}
                    </div>
                    <div className="text-xs text-slate-500 dark:text-slate-400">
                      {t('forum.by')}{' '}
                      <span
                        onClick={(e) => {
                          e.preventDefault()
                          e.stopPropagation()
                          navigate(`/users/${topic.authorId}`)
                        }}
                        className="cursor-pointer font-medium hover:underline"
                      >
                        {topic.authorName}
                      </span>{' '}
                      · {new Date(topic.createdAt).toLocaleDateString()}
                    </div>
                  </div>
                  <div className="flex flex-none gap-4 text-center text-xs text-slate-500 dark:text-slate-400">
                    <div>
                      <div className="font-bold text-[#1E2A44] dark:text-slate-200">{topic.replyCount}</div>
                      {t('forum.replies')}
                    </div>
                    <div>
                      <div className="font-bold text-[#1E2A44] dark:text-slate-200">{topic.viewCount}</div>
                      {t('forum.views')}
                    </div>
                  </div>
                </Link>
                </TopicContextMenu>
              </li>
            ))}
          </ul>
        )}
      </main>
    </div>
  )
}

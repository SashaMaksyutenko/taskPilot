import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import Avatar from '../components/Avatar'
import Navbar from '../components/Navbar'
import { forumService } from '../services/forumService'
import { useAppSelector } from '../store/hooks'
import type { Reply, TopicDetail } from '../types/forum'

/**
 * A single forum topic: the original post, its replies with voting and an
 * "accept solution" action (topic author only), plus a reply form.
 */
export default function TopicPage() {
  const { t } = useTranslation()
  const { topicId = '' } = useParams()
  const currentUserId = useAppSelector((s) => s.auth.user?.id)
  const [topic, setTopic] = useState<TopicDetail | null>(null)
  const [body, setBody] = useState('')

  const load = () => {
    if (topicId) forumService.getTopic(topicId).then(setTopic).catch(() => {})
  }

  useEffect(load, [topicId])

  const isAuthor = topic && currentUserId === topic.authorId

  const vote = async (reply: Reply, value: 1 | -1) => {
    const result = await forumService.vote(reply.id, value).catch(() => null)
    if (!result || !topic) return
    setTopic({
      ...topic,
      replies: topic.replies.map((r) =>
        r.id === reply.id ? { ...r, score: result.score, myVote: result.myVote } : r,
      ),
    })
  }

  const markSolution = async (reply: Reply) => {
    await forumService.markSolution(reply.id).catch(() => {})
    load()
  }

  const submitReply = async () => {
    if (!body.trim() || !topic) return
    await forumService.addReply({ topicId: topic.id, body: body.trim() }).catch(() => {})
    setBody('')
    load()
  }

  if (!topic) {
    return (
      <div className="min-h-screen bg-slate-50 dark:bg-slate-900">
        <Navbar />
        <p className="p-8 text-slate-400">{t('topic.loading')}</p>
      </div>
    )
  }

  return (
    <div className="min-h-screen bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      <Navbar />
      <main className="mx-auto max-w-3xl px-6 py-8">
        <Link to="/forum" className="text-sm text-slate-500 hover:underline dark:text-slate-400">
          {t('topic.backToForum')}
        </Link>

        {/* Original post */}
        <div className="mt-3 rounded-xl border border-slate-200 bg-white p-6 dark:border-slate-700 dark:bg-slate-800">
          <h1 className="text-xl font-bold">{topic.title}</h1>
          <div className="mt-1 flex items-center gap-2 text-xs text-slate-500 dark:text-slate-400">
            <Avatar name={topic.authorName} src={topic.authorAvatarUrl} size={24} />
            <span>
              {t('forum.by')}{' '}
              <Link to={`/users/${topic.authorId}`} className="font-medium hover:underline">
                {topic.authorName}
              </Link>{' '}
              · {new Date(topic.createdAt).toLocaleString()} · {topic.viewCount} {t('forum.views')}
            </span>
          </div>
          <p className="mt-4 whitespace-pre-wrap">{topic.body}</p>
        </div>

        {/* Replies */}
        <h2 className="mb-3 mt-6 font-bold">
          {t('topic.repliesHeading', { count: topic.replies.length })}
        </h2>
        <div className="space-y-3">
          {topic.replies.map((r) => (
            <div
              key={r.id}
              className={`flex gap-3 rounded-xl border bg-white p-4 dark:bg-slate-800 ${
                r.isSolution ? 'border-green-400' : 'border-slate-200 dark:border-slate-700'
              }`}
            >
              {/* Vote control */}
              <div className="flex flex-none flex-col items-center">
                <button
                  onClick={() => vote(r, 1)}
                  className={`text-lg leading-none ${r.myVote === 1 ? 'text-[#F6BE2C]' : 'text-slate-400 hover:text-slate-600'}`}
                >
                  ▲
                </button>
                <span className="text-sm font-bold">{r.score}</span>
                <button
                  onClick={() => vote(r, -1)}
                  className={`text-lg leading-none ${r.myVote === -1 ? 'text-blue-500' : 'text-slate-400 hover:text-slate-600'}`}
                >
                  ▼
                </button>
              </div>

              <div className="min-w-0 flex-1">
                {r.isSolution && (
                  <span className="mb-1 inline-block rounded bg-green-100 px-2 py-0.5 text-[11px] font-semibold text-green-700">
                    ✓ {t('topic.solution')}
                  </span>
                )}
                <p className="whitespace-pre-wrap">{r.body}</p>
                <div className="mt-2 flex items-center gap-2 text-xs text-slate-500 dark:text-slate-400">
                  <Avatar name={r.authorName} src={r.authorAvatarUrl} size={22} />
                  <Link to={`/users/${r.authorId}`} className="font-medium hover:underline">
                    {r.authorName}
                  </Link>
                  {isAuthor && !r.isSolution && (
                    <button onClick={() => markSolution(r)} className="font-semibold text-green-600 hover:underline">
                      {t('topic.markSolution')}
                    </button>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>

        {/* Add reply */}
        {topic.isLocked ? (
          <p className="mt-6 text-sm text-slate-400">🔒 {t('topic.locked')}</p>
        ) : (
          <div className="mt-6">
            <textarea
              value={body}
              onChange={(e) => setBody(e.target.value)}
              placeholder={t('topic.replyPlaceholder')}
              rows={3}
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-800"
            />
            <button
              onClick={submitReply}
              className="mt-2 rounded-lg bg-[#1E2A44] px-5 py-2 font-semibold text-white transition hover:bg-[#27345a]"
            >
              {t('topic.reply')}
            </button>
          </div>
        )}
      </main>
    </div>
  )
}

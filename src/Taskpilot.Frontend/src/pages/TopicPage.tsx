import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import Avatar from '../components/Avatar'
import Markdown from '../components/Markdown'
import MarkdownEditor from '../components/MarkdownEditor'
import ConfirmDialog from '../components/ConfirmDialog'
import TagInput from '../components/TagInput'
import ActionsContextMenu, { type ContextAction } from '../components/ActionsContextMenu'
import { apiErrorMessage } from '../lib/apiError'
import { notify } from '../lib/toast'
import { forumService } from '../services/forumService'
import { useAppSelector } from '../store/hooks'
import type { Reply, TopicDetail } from '../types/forum'

// Quick emoji reactions offered on each reply.
const REACTION_EMOJIS = ['👍', '👎', '❤️', '🔥', '🎉', '😂', '😮', '😢', '🙏', '👏']

// How many replies to show per page (all are loaded; pagination is client-side).
const REPLIES_PER_PAGE = 10

/**
 * A single forum topic: the original post, its replies with voting, "accept
 * solution", quoting/replying to a specific message, inline editing and deletion.
 */
export default function TopicPage() {
  const { t } = useTranslation()
  const { topicId = '' } = useParams()
  const currentUser = useAppSelector((s) => s.auth.user)
  const currentUserId = currentUser?.id
  const isAdmin = currentUser?.role === 'Admin'
  const [topic, setTopic] = useState<TopicDetail | null>(null)
  const [body, setBody] = useState('')
  const [error, setError] = useState('')
  // Reply currently being edited inline, and its working text.
  const [editingReplyId, setEditingReplyId] = useState<string | null>(null)
  const [editBody, setEditBody] = useState('')
  // The reply this new message is answering (drives the "replying to X" hint).
  const [replyingTo, setReplyingTo] = useState<Reply | null>(null)
  // Reply awaiting delete confirmation.
  const [deletingReply, setDeletingReply] = useState<Reply | null>(null)
  // Reply being reported, and the reason being typed.
  const [reportingReply, setReportingReply] = useState<Reply | null>(null)
  const [reportReason, setReportReason] = useState('')
  // Reply whose emoji picker is open (null = none).
  const [reactionPickerFor, setReactionPickerFor] = useState<string | null>(null)
  // Topic (original post) inline edit state.
  const [editingTopic, setEditingTopic] = useState(false)
  const [editTitle, setEditTitle] = useState('')
  const [editTopicBody, setEditTopicBody] = useState('')
  const [editTags, setEditTags] = useState<string[]>([])
  // Editor container, so replying/quoting can scroll it into view.
  const replyEditorRef = useRef<HTMLDivElement | null>(null)
  // Client-side pagination of the replies list.
  const [replyPage, setReplyPage] = useState(1)

  const load = () => {
    if (topicId) forumService.getTopic(topicId).then(setTopic).catch(() => {})
  }

  useEffect(load, [topicId])

  // Count exactly one view per opened topic. The ref guard makes it fire once even
  // under React StrictMode's double-invoke, and re-fetches (vote/reply) don't count.
  const viewedTopicId = useRef<string | null>(null)
  useEffect(() => {
    if (!topicId || viewedTopicId.current === topicId) return
    viewedTopicId.current = topicId
    forumService.incrementView(topicId).catch(() => {})
  }, [topicId])

  // Keep the reply page within range as replies are added or removed.
  useEffect(() => {
    const pages = Math.max(1, Math.ceil((topic?.replies.length ?? 0) / REPLIES_PER_PAGE))
    setReplyPage((p) => Math.min(p, pages))
  }, [topic?.replies.length])

  const isAuthor = topic && currentUserId === topic.authorId
  const canEditTopic = !!topic && (currentUserId === topic.authorId || isAdmin)

  // Look up a reply by id (used to render "X wrote:" quote references).
  const replyById = (id: string | null): Reply | undefined =>
    id ? topic?.replies.find((r) => r.id === id) : undefined

  const replyTotalPages = Math.max(1, Math.ceil((topic?.replies.length ?? 0) / REPLIES_PER_PAGE))
  const visibleReplies = topic ? topic.replies.slice((replyPage - 1) * REPLIES_PER_PAGE, replyPage * REPLIES_PER_PAGE) : []

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

  // A user can edit/delete a reply if they wrote it, or if they are an admin.
  const canModifyReply = (reply: Reply) => currentUserId === reply.authorId || isAdmin

  const startEditReply = (reply: Reply) => {
    setEditingReplyId(reply.id)
    setEditBody(reply.body)
    setError('')
  }

  const cancelEditReply = () => {
    setEditingReplyId(null)
    setEditBody('')
  }

  const saveEditReply = async (reply: Reply) => {
    if (!editBody.trim() || !topic) return
    setError('')
    try {
      const updated = await forumService.editReply(reply.id, editBody.trim())
      setTopic({
        ...topic,
        replies: topic.replies.map((r) => (r.id === reply.id ? { ...r, body: updated.body, updatedAt: updated.updatedAt } : r)),
      })
      cancelEditReply()
    } catch (e) {
      setError(apiErrorMessage(e))
    }
  }

  const removeReply = async (reply: Reply) => {
    await forumService.deleteReply(reply.id).catch(() => {})
    if (!topic) return
    // Deleted replies simply disappear from view.
    setTopic({ ...topic, replies: topic.replies.filter((r) => r.id !== reply.id) })
  }

  const submitReport = async () => {
    if (!reportingReply) return
    await forumService.reportReply(reportingReply.id, reportReason.trim() || undefined).catch(() => {})
    setReportingReply(null)
    setReportReason('')
    notify.success(t('topic.reportSent'))
  }

  const toggleReaction = async (reply: Reply, emoji: string) => {
    const reactions = await forumService.reactToReply(reply.id, emoji).catch(() => null)
    if (!reactions || !topic) return
    setTopic({
      ...topic,
      replies: topic.replies.map((r) => (r.id === reply.id ? { ...r, reactions } : r)),
    })
    setReactionPickerFor(null)
  }

  // Bring the reply editor into view and focus it after choosing a target.
  const focusEditor = () => {
    replyEditorRef.current?.scrollIntoView({ behavior: 'smooth', block: 'center' })
    replyEditorRef.current?.querySelector('textarea')?.focus()
  }

  const startReplyTo = (reply: Reply) => {
    setReplyingTo(reply)
    focusEditor()
  }

  // Quote a reply: set it as the parent and prefill the editor with its text as
  // a Markdown blockquote attributed to the author.
  const quoteReply = (reply: Reply) => {
    setReplyingTo(reply)
    const quoted = reply.body
      .split('\n')
      .map((line) => `> ${line}`)
      .join('\n')
    const header = `> **${reply.authorName} ${t('topic.wrote')}**`
    setBody((prev) => `${header}\n${quoted}\n\n${prev}`)
    focusEditor()
  }

  const submitReply = async () => {
    if (!body.trim() || !topic) return
    setError('')
    try {
      await forumService.addReply({
        topicId: topic.id,
        body: body.trim(),
        parentReplyId: replyingTo?.id,
      })
      setBody('')
      setReplyingTo(null)
      load()
      // New replies land at the end, so jump to the last page (clamped by the effect).
      setReplyPage(Number.MAX_SAFE_INTEGER)
    } catch (e) {
      setError(apiErrorMessage(e))
    }
  }

  const startEditTopic = () => {
    if (!topic) return
    setEditTitle(topic.title)
    setEditTopicBody(topic.body)
    setEditTags(topic.tags)
    setEditingTopic(true)
    setError('')
  }

  const saveEditTopic = async () => {
    if (!topic || !editTitle.trim() || !editTopicBody.trim()) return
    setError('')
    try {
      const updated = await forumService.editTopic(topic.id, {
        title: editTitle.trim(),
        body: editTopicBody.trim(),
        tags: editTags,
      })
      setTopic({ ...topic, title: updated.title, body: updated.body, updatedAt: updated.updatedAt, tags: updated.tags })
      setEditingTopic(false)
    } catch (e) {
      setError(apiErrorMessage(e))
    }
  }

  const toggleSubscribe = async () => {
    if (!topic) return
    const subscribed = await forumService.toggleSubscription(topic.id).catch(() => null)
    if (subscribed === null) return
    setTopic({ ...topic, isSubscribed: subscribed })
  }

  const togglePin = async () => {
    if (!topic) return
    await forumService.setPinned(topic.id, !topic.isPinned).catch(() => {})
    setTopic({ ...topic, isPinned: !topic.isPinned })
  }

  const toggleLock = async () => {
    if (!topic) return
    await forumService.setLocked(topic.id, !topic.isLocked).catch(() => {})
    setTopic({ ...topic, isLocked: !topic.isLocked })
  }

  if (!topic) {
    return <p className="text-muted">{t('topic.loading')}</p>
  }

  return (
    <div className="mx-auto max-w-3xl px-6 py-8">
        <Link to="/forum" className="text-sm text-slate-500 hover:underline dark:text-slate-400">
          {t('topic.backToForum')}
        </Link>

        {/* Original post */}
        <div className="mt-3 rounded-xl border border-slate-200 bg-white p-6 dark:border-slate-700 dark:bg-slate-800">
          {editingTopic ? (
            <div>
              <input
                value={editTitle}
                onChange={(e) => setEditTitle(e.target.value)}
                placeholder={t('forum.topicTitle')}
                className="mb-2 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 font-bold outline-none focus:border-primary dark:border-slate-600 dark:bg-slate-900"
              />
              <MarkdownEditor
                value={editTopicBody}
                onChange={setEditTopicBody}
                placeholder={t('forum.bodyPlaceholder')}
                className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-primary dark:border-slate-600 dark:bg-slate-900"
              />
              <div className="mt-2">
                <TagInput tags={editTags} onChange={setEditTags} />
              </div>
              <div className="mt-2 flex items-center gap-3 text-sm">
                <button
                  onClick={saveEditTopic}
                  className="rounded-lg bg-primary px-4 py-1.5 font-semibold text-white transition hover:bg-primary-hover"
                >
                  {t('topic.save')}
                </button>
                <button onClick={() => setEditingTopic(false)} className="font-semibold text-slate-500 hover:underline">
                  {t('topic.cancel')}
                </button>
                {error && <span className="font-medium text-red-600">{error}</span>}
              </div>
            </div>
          ) : (
            <>
              <div className="flex items-start gap-2">
                <h1 className="flex-1 text-xl font-bold">
                  {topic.isPinned && <span className="mr-1">📌</span>}
                  {topic.isLocked && <span className="mr-1" title={t('topic.locked')}>🔒</span>}
                  {topic.title}
                </h1>
                <div className="flex flex-none items-center gap-3 text-sm font-semibold">
                  <button onClick={toggleSubscribe} className="text-primary hover:underline dark:text-slate-200">
                    {topic.isSubscribed ? t('topic.unsubscribe') : t('topic.subscribe')}
                  </button>
                  {isAdmin && (
                    <button onClick={togglePin} className="text-primary hover:underline dark:text-slate-200">
                      {topic.isPinned ? t('forum.unpin') : t('forum.pin')}
                    </button>
                  )}
                  {canEditTopic && (
                    <button onClick={toggleLock} className="text-primary hover:underline dark:text-slate-200">
                      {topic.isLocked ? t('forum.unlock') : t('forum.lock')}
                    </button>
                  )}
                  {canEditTopic && (
                    <button onClick={startEditTopic} className="text-primary hover:underline dark:text-slate-200">
                      {t('topic.edit')}
                    </button>
                  )}
                </div>
              </div>
              <div className="mt-1 flex items-center gap-2 text-xs text-slate-500 dark:text-slate-400">
                <Avatar name={topic.authorName} src={topic.authorAvatarUrl} size={24} />
                <span>
                  {t('forum.by')}{' '}
                  <Link to={`/users/${topic.authorId}`} className="font-medium hover:underline">
                    {topic.authorName}
                  </Link>{' '}
                  · {new Date(topic.createdAt).toLocaleString()} · {topic.viewCount} {t('forum.views')}
                  {topic.updatedAt && <span className="italic"> · {t('topic.edited')}</span>}
                </span>
              </div>
              {topic.tags.length > 0 && (
                <div className="mt-2 flex flex-wrap gap-1.5">
                  {topic.tags.map((tag) => (
                    <Link
                      key={tag}
                      to={`/forum?tag=${encodeURIComponent(tag)}`}
                      className="rounded-full bg-primary/10 px-2 py-0.5 text-xs font-medium text-primary hover:bg-primary/20 dark:bg-slate-700 dark:text-slate-200"
                    >
                      #{tag}
                    </Link>
                  ))}
                </div>
              )}
              <div className="mt-4">
                <Markdown>{topic.body}</Markdown>
              </div>
            </>
          )}
        </div>

        {/* Replies */}
        <h2 className="mb-3 mt-6 font-bold">
          {t('topic.repliesHeading', { count: topic.replies.length })}
        </h2>
        <div className="space-y-3">
          {visibleReplies.map((r) => {
            const parent = replyById(r.parentReplyId)
            const replyActions: ContextAction[] = [
              { label: t('menu.copyText'), onSelect: () => navigator.clipboard?.writeText(r.body).catch(() => {}) },
            ]
            if (!topic.isLocked) {
              replyActions.push({ label: t('topic.replyTo'), onSelect: () => startReplyTo(r) })
              replyActions.push({ label: t('topic.quote'), onSelect: () => quoteReply(r) })
            }
            if (canModifyReply(r)) {
              replyActions.push({ label: t('topic.edit'), onSelect: () => startEditReply(r) })
            }
            if (isAuthor && !r.isSolution) {
              replyActions.push({ label: t('topic.markSolution'), onSelect: () => markSolution(r) })
            }
            if (currentUserId !== r.authorId) {
              replyActions.push({ label: t('topic.report'), onSelect: () => setReportingReply(r) })
            }
            if (canModifyReply(r)) {
              replyActions.push({ label: t('topic.delete'), onSelect: () => setDeletingReply(r), danger: true })
            }
            return (
            <ActionsContextMenu key={r.id} actions={replyActions}>
            <div
              id={`reply-${r.id}`}
              className={`flex gap-3 rounded-xl border bg-white p-4 dark:bg-slate-800 ${
                r.isSolution ? 'border-green-400' : 'border-slate-200 dark:border-slate-700'
              }`}
            >
              {/* Vote control */}
              <div className="flex flex-none flex-col items-center">
                <button
                  onClick={() => vote(r, 1)}
                  className={`text-lg leading-none ${r.myVote === 1 ? 'text-accent' : 'text-muted hover:text-foreground'}`}
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

                {/* "X wrote:" reference to the parent reply this one answers */}
                {parent && (
                  <a
                    href={`#reply-${parent.id}`}
                    className="mb-2 block rounded-lg border-l-4 border-slate-300 bg-slate-50 px-3 py-1.5 text-xs text-slate-500 hover:bg-slate-100 dark:border-slate-600 dark:bg-slate-900/40 dark:text-slate-400"
                  >
                    <span className="font-semibold">{parent.authorName} {t('topic.wrote')}</span>{' '}
                    <span className="line-clamp-2">{parent.body}</span>
                  </a>
                )}

                {editingReplyId === r.id ? (
                  <div>
                    <MarkdownEditor
                      value={editBody}
                      onChange={setEditBody}
                      placeholder={t('topic.replyPlaceholder')}
                      className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-primary dark:border-slate-600 dark:bg-slate-900"
                    />
                    <div className="mt-2 flex items-center gap-3 text-xs">
                      <button
                        onClick={() => saveEditReply(r)}
                        className="rounded-lg bg-primary px-3 py-1 font-semibold text-white transition hover:bg-primary-hover"
                      >
                        {t('topic.save')}
                      </button>
                      <button onClick={cancelEditReply} className="font-semibold text-slate-500 hover:underline">
                        {t('topic.cancel')}
                      </button>
                    </div>
                  </div>
                ) : (
                  <Markdown>{r.body}</Markdown>
                )}

                {/* Emoji reactions */}
                <div className="mt-2 flex flex-wrap items-center gap-1.5">
                  {r.reactions.map((re) => (
                    <button
                      key={re.emoji}
                      onClick={() => toggleReaction(r, re.emoji)}
                      className={`flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs transition ${
                        re.mine
                          ? 'border-primary bg-primary/10 dark:border-slate-300 dark:bg-slate-700'
                          : 'border-slate-200 hover:bg-slate-100 dark:border-slate-600 dark:hover:bg-slate-700'
                      }`}
                    >
                      <span>{re.emoji}</span>
                      <span className="font-semibold">{re.count}</span>
                    </button>
                  ))}
                  <div className="relative">
                    <button
                      onClick={() => setReactionPickerFor(reactionPickerFor === r.id ? null : r.id)}
                      className="rounded-full border border-slate-200 px-2 py-0.5 text-xs text-slate-400 hover:bg-slate-100 dark:border-slate-600 dark:hover:bg-slate-700"
                      aria-label={t('topic.addReaction')}
                    >
                      🙂+
                    </button>
                    {reactionPickerFor === r.id && (
                      <div className="absolute z-10 mt-1 flex w-52 flex-wrap gap-1 rounded-lg border border-slate-200 bg-white p-2 shadow-lg dark:border-slate-600 dark:bg-slate-800">
                        {REACTION_EMOJIS.map((e) => (
                          <button
                            key={e}
                            onClick={() => toggleReaction(r, e)}
                            className="rounded p-1 text-lg hover:bg-slate-100 dark:hover:bg-slate-700"
                          >
                            {e}
                          </button>
                        ))}
                      </div>
                    )}
                  </div>
                </div>

                <div className="mt-2 flex flex-wrap items-center gap-2 text-xs text-slate-500 dark:text-slate-400">
                  <Avatar name={r.authorName} src={r.authorAvatarUrl} size={22} />
                  <Link to={`/users/${r.authorId}`} className="font-medium hover:underline">
                    {r.authorName}
                  </Link>
                  <span>· {new Date(r.createdAt).toLocaleString()}</span>
                  {r.updatedAt && <span className="italic">· {t('topic.edited')}</span>}
                  {!topic.isLocked && editingReplyId !== r.id && (
                    <>
                      <button onClick={() => startReplyTo(r)} className="font-semibold text-primary hover:underline dark:text-slate-200">
                        {t('topic.replyTo')}
                      </button>
                      <button onClick={() => quoteReply(r)} className="font-semibold text-primary hover:underline dark:text-slate-200">
                        {t('topic.quote')}
                      </button>
                    </>
                  )}
                  {canModifyReply(r) && editingReplyId !== r.id && (
                    <button onClick={() => startEditReply(r)} className="font-semibold text-primary hover:underline dark:text-slate-200">
                      {t('topic.edit')}
                    </button>
                  )}
                  {isAuthor && !r.isSolution && (
                    <button onClick={() => markSolution(r)} className="font-semibold text-green-600 hover:underline">
                      {t('topic.markSolution')}
                    </button>
                  )}
                  {currentUserId !== r.authorId && (
                    <button onClick={() => setReportingReply(r)} className="font-semibold text-slate-400 hover:text-red-600 hover:underline">
                      {t('topic.report')}
                    </button>
                  )}
                  {canModifyReply(r) && (
                    <button onClick={() => setDeletingReply(r)} className="font-semibold text-red-600 hover:underline">
                      {t('topic.delete')}
                    </button>
                  )}
                </div>
              </div>
            </div>
            </ActionsContextMenu>
            )
          })}
        </div>

        {/* Replies pagination */}
        {replyTotalPages > 1 && (
          <div className="mt-4 flex items-center justify-center gap-4 text-sm">
            <button
              onClick={() => setReplyPage((p) => Math.max(1, p - 1))}
              disabled={replyPage <= 1}
              className="rounded-lg border border-slate-300 px-4 py-1.5 font-semibold transition hover:bg-white disabled:opacity-40 dark:border-slate-600 dark:hover:bg-slate-800"
            >
              {t('audit.prev')}
            </button>
            <span className="text-slate-500 dark:text-slate-400">
              {t('audit.pageOf', { page: replyPage, total: replyTotalPages })}
            </span>
            <button
              onClick={() => setReplyPage((p) => Math.min(replyTotalPages, p + 1))}
              disabled={replyPage >= replyTotalPages}
              className="rounded-lg border border-slate-300 px-4 py-1.5 font-semibold transition hover:bg-white disabled:opacity-40 dark:border-slate-600 dark:hover:bg-slate-800"
            >
              {t('audit.next')}
            </button>
          </div>
        )}

        {/* Add reply */}
        {topic.isLocked ? (
          <p className="mt-6 text-sm text-slate-400">🔒 {t('topic.locked')}</p>
        ) : (
          <div className="mt-6" ref={replyEditorRef}>
            {replyingTo && (
              <div className="mb-2 flex items-center gap-2 rounded-lg bg-primary/5 px-3 py-1.5 text-sm dark:bg-slate-800">
                <span className="text-slate-500 dark:text-slate-400">
                  {t('topic.replyingTo')} <span className="font-semibold">{replyingTo.authorName}</span>
                </span>
                <button onClick={() => setReplyingTo(null)} className="ml-auto text-slate-400 hover:text-red-600" aria-label={t('topic.cancel')}>
                  ✕
                </button>
              </div>
            )}
            <MarkdownEditor
              value={body}
              onChange={setBody}
              placeholder={t('topic.replyPlaceholder')}
              className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-primary dark:border-slate-600 dark:bg-slate-800"
            />
            <div className="mt-2 flex items-center gap-3">
              <button
                onClick={submitReply}
                className="rounded-lg bg-primary px-5 py-2 font-semibold text-white transition hover:bg-primary-hover"
              >
                {t('topic.reply')}
              </button>
              {error && <span className="text-sm font-medium text-red-600">{error}</span>}
            </div>
          </div>
        )}

        {/* Reply delete confirmation */}
        <ConfirmDialog
          open={!!deletingReply}
          title={t('topic.deleteTitle')}
          message={t('topic.deleteConfirm')}
          onConfirm={() => {
            if (deletingReply) removeReply(deletingReply)
            setDeletingReply(null)
          }}
          onCancel={() => setDeletingReply(null)}
        />

        {/* Report a reply */}
        {reportingReply && (
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={() => setReportingReply(null)}>
            <div
              className="w-full max-w-md rounded-xl border border-slate-200 bg-white p-5 shadow-xl dark:border-slate-700 dark:bg-slate-800"
              onClick={(e) => e.stopPropagation()}
            >
              <h3 className="mb-1 font-bold">{t('topic.reportTitle')}</h3>
              <p className="mb-3 text-sm text-slate-500 dark:text-slate-400">{t('topic.reportHint')}</p>
              <textarea
                value={reportReason}
                onChange={(e) => setReportReason(e.target.value)}
                placeholder={t('topic.reportPlaceholder')}
                rows={3}
                maxLength={1000}
                className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-primary dark:border-slate-600 dark:bg-slate-900"
              />
              <div className="mt-3 flex justify-end gap-3 text-sm">
                <button onClick={() => setReportingReply(null)} className="font-semibold text-slate-500 hover:underline">
                  {t('topic.cancel')}
                </button>
                <button
                  onClick={submitReport}
                  className="rounded-lg bg-red-600 px-4 py-1.5 font-semibold text-white transition hover:bg-red-700"
                >
                  {t('topic.reportSubmit')}
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
  )
}

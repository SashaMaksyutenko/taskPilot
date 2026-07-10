import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import Avatar from '../components/Avatar'
import Markdown from '../components/Markdown'
import StarRating from '../components/StarRating'
import { marketplaceService } from '../services/marketplaceService'
import { notify } from '../lib/toast'
import { celebrate } from '../lib/confetti'
import { useAppSelector } from '../store/hooks'
import type { MarketTaskDetail, Review } from '../types/marketplace'

const appStatusColor: Record<string, string> = {
  Pending: 'bg-amber-100 text-amber-700',
  Accepted: 'bg-green-100 text-green-700',
  Rejected: 'bg-red-100 text-red-700',
}

/**
 * One marketplace task. The poster sees and decides on applications; everyone
 * else can apply (while the task is open and they haven't applied yet).
 */
export default function MarketplaceTaskPage() {
  const { t } = useTranslation()
  const { taskId = '' } = useParams()
  const [searchParams, setSearchParams] = useSearchParams()
  const currentUserId = useAppSelector((s) => s.auth.user?.id)
  const [task, setTask] = useState<MarketTaskDetail | null>(null)
  const [coverLetter, setCoverLetter] = useState('')
  const [rate, setRate] = useState('')
  const [reviews, setReviews] = useState<Review[]>([])
  const [myStars, setMyStars] = useState(0)
  const [myComment, setMyComment] = useState('')

  const load = () => {
    if (taskId) marketplaceService.getTask(taskId).then(setTask).catch(() => {})
  }

  const loadReviews = () => {
    if (taskId) marketplaceService.getReviews(taskId).then(setReviews).catch(() => {})
  }

  useEffect(load, [taskId])
  useEffect(loadReviews, [taskId])

  // Confirm a payment once the poster is back from Stripe. Fires both on the
  // explicit ?paid=1 return and whenever a payment is still Pending (so a lost
  // redirect still settles on the next visit). Confirm only succeeds once Stripe
  // reports the session as paid, so this is safe to retry.
  useEffect(() => {
    if (!task) return
    const isPosterHere = currentUserId === task.posterId
    const justPaid = searchParams.get('paid') === '1'
    if (!isPosterHere || (task.paymentStatus !== 'Pending' && !justPaid)) return
    marketplaceService
      .confirmPayment(task.id)
      .then(() => {
        if (justPaid) {
          notify.success(t('marketTask.paymentConfirmed'))
          celebrate()
        }
        load()
      })
      .catch(() => {})
      .finally(() => {
        if (justPaid) setSearchParams({}, { replace: true })
      })
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [task?.id, task?.paymentStatus, currentUserId])

  const submitRating = async () => {
    if (myStars < 1) return
    await marketplaceService.rate(taskId, myStars, myComment.trim() || undefined).catch(() => {})
    setMyStars(0)
    setMyComment('')
    loadReviews()
  }

  if (!task) {
    return <p className="text-muted">{t('topic.loading')}</p>
  }

  const isPoster = currentUserId === task.posterId
  const myApplication = task.applications.find((a) => a.applicantId === currentUserId)

  const apply = async () => {
    if (!coverLetter.trim() || !rate) return
    await marketplaceService
      .apply({ taskId: task.id, coverLetter: coverLetter.trim(), proposedRate: Number(rate) })
      .catch(() => {})
    setCoverLetter('')
    setRate('')
    load()
  }

  const decide = async (applicationId: string, accept: boolean) => {
    if (accept) await marketplaceService.accept(applicationId).catch(() => {})
    else await marketplaceService.reject(applicationId).catch(() => {})
    load()
  }

  const submitWork = async () => {
    await marketplaceService.submit(task!.id).catch(() => {})
    load()
  }

  const approveWork = async () => {
    await marketplaceService.approve(task!.id).catch(() => {})
    load()
  }

  const payTask = async () => {
    try {
      const url = await marketplaceService.pay(task!.id)
      window.location.href = url  // Redirect to Stripe's hosted checkout.
    } catch {
      notify.error(t('marketTask.paymentUnavailable'))
    }
  }

  return (
    <div className="mx-auto max-w-3xl px-6 py-8">
        <Link to="/marketplace" className="text-sm text-slate-500 hover:underline dark:text-slate-400">
          {t('marketTask.back')}
        </Link>

        {/* Task header */}
        <div className="mt-3 rounded-xl border border-slate-200 bg-white p-6 dark:border-slate-700 dark:bg-slate-800">
          <div className="flex items-start gap-3">
            <h1 className="flex-1 text-xl font-bold">{task.title}</h1>
            <span className="text-lg font-bold">${task.budget}</span>
          </div>
          <div className="mt-1 flex items-center gap-2 text-xs text-slate-500 dark:text-slate-400">
            <Avatar name={task.posterName} src={task.posterAvatarUrl} size={24} />
            <span>
              {t('forum.by')}{' '}
              <Link to={`/users/${task.posterId}`} className="font-medium hover:underline">{task.posterName}</Link>
              {' · '}{t(`market.status.${task.status}`, task.status)}
              {task.assigneeName ? ` · ${t('marketTask.assignedTo', { name: task.assigneeName })}` : ''}
            </span>
          </div>
          {task.requiredSkills && (
            <div className="mt-2 text-sm text-slate-600 dark:text-slate-300">{t('marketTask.skills')}: {task.requiredSkills}</div>
          )}
          <div className="mt-4">
            <Markdown>{task.description}</Markdown>
          </div>
        </div>

        {/* Payment — on a completed task */}
        {task.status === 'Completed' && (
          <>
            {task.paymentStatus === 'Paid' ? (
              <div className="mt-6 flex items-center gap-2 rounded-xl border border-green-300 bg-green-50 p-4 text-sm font-semibold text-green-700 dark:border-green-700 dark:bg-green-950/30 dark:text-green-300">
                ✓ {t('marketTask.paid', { amount: task.budget })}
              </div>
            ) : isPoster ? (
              <div className="mt-6 flex items-center gap-3 rounded-xl border border-slate-200 bg-white p-4 dark:border-slate-700 dark:bg-slate-800">
                <span className="text-sm">{t('marketTask.payPrompt', { name: task.assigneeName ?? '' })}</span>
                <button
                  onClick={payTask}
                  className="ml-auto rounded-lg bg-[#635BFF] px-5 py-2 text-sm font-semibold text-white hover:opacity-90"
                >
                  {t('marketTask.pay', { amount: task.budget })}
                </button>
              </div>
            ) : null}
          </>
        )}

        {/* Poster: applications */}
        {isPoster ? (
          <>
            {task.status === 'Submitted' && (
              <div className="mt-6 flex items-center gap-3 rounded-xl border border-amber-300 bg-amber-50 p-4 dark:border-amber-700 dark:bg-amber-950/30">
                <span className="text-sm">{t('market.status.Submitted')}</span>
                <button
                  onClick={approveWork}
                  className="ml-auto rounded-lg bg-green-600 px-5 py-2 text-sm font-semibold text-white hover:bg-green-700"
                >
                  {t('marketTask.approveWork')}
                </button>
              </div>
            )}
            <h2 className="mb-3 mt-6 font-bold">{t('market.applications', { count: task.applications.length })}</h2>
            <div className="space-y-3">
              {task.applications.map((a) => (
                <div key={a.id} className="rounded-xl border border-slate-200 bg-white p-4 dark:border-slate-700 dark:bg-slate-800">
                  <div className="flex items-center gap-2">
                    <Avatar name={a.applicantName} src={a.applicantAvatarUrl} size={28} />
                    <Link to={`/users/${a.applicantId}`} className="font-semibold hover:underline">{a.applicantName}</Link>
                    <span className="text-sm text-slate-500 dark:text-slate-400">· ${a.proposedRate}</span>
                    <span className={`ml-auto rounded-full px-2 py-0.5 text-[11px] font-semibold ${appStatusColor[a.status]}`}>
                      {t(`marketTask.appStatus.${a.status}`, a.status)}
                    </span>
                  </div>
                  <p className="mt-2 text-sm">{a.coverLetter}</p>
                  {a.status === 'Pending' && task.status === 'Open' && (
                    <div className="mt-3 flex gap-2">
                      <button onClick={() => decide(a.id, true)} className="rounded-lg bg-green-600 px-4 py-1.5 text-sm font-semibold text-white hover:bg-green-700">
                        {t('marketTask.accept')}
                      </button>
                      <button onClick={() => decide(a.id, false)} className="rounded-lg border border-slate-300 px-4 py-1.5 text-sm font-semibold hover:bg-slate-50 dark:border-slate-600 dark:hover:bg-slate-700">
                        {t('marketTask.reject')}
                      </button>
                    </div>
                  )}
                </div>
              ))}
              {task.applications.length === 0 && <p className="text-slate-400">{t('marketTask.noApplications')}</p>}
            </div>
          </>
        ) : myApplication ? (
          <div className="mt-6 rounded-xl border border-slate-200 bg-white p-5 dark:border-slate-700 dark:bg-slate-800">
            {t('marketTask.youApplied')}{' '}
            <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${appStatusColor[myApplication.status]}`}>
              {t(`marketTask.appStatus.${myApplication.status}`, myApplication.status)}
            </span>
            {/* Assignee can submit their finished work */}
            {task.assigneeId === currentUserId && task.status === 'InProgress' && (
              <div className="mt-3">
                <button
                  onClick={submitWork}
                  className="rounded-lg bg-[#1E2A44] px-5 py-2 text-sm font-semibold text-white hover:bg-[#27345a]"
                >
                  {t('marketTask.submitWork')}
                </button>
              </div>
            )}
          </div>
        ) : task.status === 'Open' ? (
          <div className="mt-6 rounded-xl border border-slate-200 bg-white p-5 dark:border-slate-700 dark:bg-slate-800">
            <h2 className="mb-3 font-bold">{t('marketTask.applyHeading')}</h2>
            <textarea
              value={coverLetter}
              onChange={(e) => setCoverLetter(e.target.value)}
              placeholder={t('marketTask.coverLetter')}
              rows={3}
              className="mb-2 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
            />
            <div className="flex gap-2">
              <input
                value={rate}
                onChange={(e) => setRate(e.target.value)}
                type="number"
                placeholder={t('marketTask.rate')}
                className="w-40 rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
              />
              <button onClick={apply} className="rounded-lg bg-[#1E2A44] px-5 font-semibold text-white hover:bg-[#27345a]">
                {t('marketTask.apply')}
              </button>
            </div>
          </div>
        ) : (
          <p className="mt-6 text-sm text-slate-400">{t('marketTask.closed')}</p>
        )}

        {/* Two-way rating — only on completed tasks */}
        {task.status === 'Completed' && (
          <div className="mt-6 rounded-xl border border-slate-200 bg-white p-5 dark:border-slate-700 dark:bg-slate-800">
            {/* Rating form: poster or assignee, if they haven't rated yet */}
            {(isPoster || task.assigneeId === currentUserId) &&
              !reviews.some((r) => r.raterId === currentUserId) && (
                <div className="mb-4 border-b border-slate-100 pb-4 dark:border-slate-700">
                  <h2 className="mb-2 font-bold">{t('marketTask.rateHeading')}</h2>
                  <StarRating value={myStars} onChange={setMyStars} />
                  <textarea
                    value={myComment}
                    onChange={(e) => setMyComment(e.target.value)}
                    placeholder={t('marketTask.rateComment')}
                    rows={2}
                    className="mt-2 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm outline-none focus:border-[#1E2A44] dark:border-slate-600 dark:bg-slate-900"
                  />
                  <button
                    onClick={submitRating}
                    disabled={myStars < 1}
                    className="mt-2 rounded-lg bg-[#1E2A44] px-5 py-2 text-sm font-semibold text-white hover:bg-[#27345a] disabled:opacity-50"
                  >
                    {t('marketTask.rateSubmit')}
                  </button>
                </div>
              )}

            {/* Existing reviews */}
            <h2 className="mb-2 font-bold">{t('marketTask.reviews')}</h2>
            {reviews.length === 0 ? (
              <p className="text-sm text-slate-400">{t('marketTask.noReviews')}</p>
            ) : (
              <ul className="space-y-3">
                {reviews.map((r) => (
                  <li key={r.id} className="text-sm">
                    <div className="flex items-center gap-2">
                      <Avatar name={r.raterName} src={r.raterAvatarUrl} size={24} />
                      <Link to={`/users/${r.raterId}`} className="font-semibold hover:underline">{r.raterName}</Link>
                      <StarRating value={r.stars} />
                    </div>
                    {r.comment && <p className="mt-1 text-slate-600 dark:text-slate-300">{r.comment}</p>}
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </div>
  )
}

import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import Avatar from './Avatar'
import StarRating from './StarRating'
import { reviewService, type UserReview } from '../services/reviewService'
import type { SharedProject } from '../services/userService'

/** Small coloured badge naming the context a review was left in. */
function ContextChip({ review }: { review: UserReview }) {
  const { t } = useTranslation()
  const label = t(`review.context.${review.context}`, review.context)
  const body = (
    <>
      <span className="font-semibold">{label}</span>
      {review.contextLabel && <span className="text-muted"> · {review.contextLabel}</span>}
    </>
  )
  const className = 'inline-flex max-w-full items-center gap-1 truncate rounded-full bg-canvas px-2 py-0.5 text-[11px]'
  return review.contextLink ? (
    <Link to={review.contextLink} className={`${className} hover:text-primary`}>
      {body}
    </Link>
  ) : (
    <span className={className}>{body}</span>
  )
}

/**
 * The "Reviews" section on a user's profile: the reviews they have received across every
 * context, plus — when you view someone else and share a project with them — a form to leave a
 * project review. Leaving a review refreshes the list and asks the page to refresh the header
 * rating via {@link onReviewLeft}.
 */
export default function ProfileReviews({
  userId,
  isOwnProfile,
  sharedProjects,
  onReviewLeft,
}: {
  userId: string
  isOwnProfile: boolean
  sharedProjects: SharedProject[]
  onReviewLeft?: () => void
}) {
  const { t } = useTranslation()
  const [reviews, setReviews] = useState<UserReview[]>([])
  const [projectId, setProjectId] = useState('')
  const [stars, setStars] = useState(0)
  const [comment, setComment] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!userId) return
    reviewService.getUserReviews(userId).then(setReviews).catch(() => {})
  }, [userId])

  // You can review someone you share at least one project with (but not yourself).
  const canReview = !isOwnProfile && sharedProjects.length > 0
  const effectiveProjectId = projectId || (sharedProjects.length === 1 ? sharedProjects[0].id : '')

  const submit = async () => {
    if (!effectiveProjectId || stars < 1 || submitting) return
    setSubmitting(true)
    setError(null)
    try {
      const created = await reviewService.leaveProjectReview(effectiveProjectId, {
        rateeId: userId,
        stars,
        comment: comment.trim() || null,
      })
      setReviews((prev) => [created, ...prev])
      setStars(0)
      setComment('')
      setProjectId('')
      onReviewLeft?.()
    } catch (e) {
      const message = (e as { response?: { data?: { error?: string } } })?.response?.data?.error
      setError(message ?? t('review.failed'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="mt-6 rounded-xl border border-border bg-surface p-6">
      <h2 className="mb-3 font-bold">{t('profile.reviews')}</h2>

      {canReview && (
        <div className="mb-4 rounded-lg border border-border bg-canvas/50 p-4">
          <h3 className="mb-2 text-sm font-semibold">{t('review.leave')}</h3>
          {sharedProjects.length > 1 && (
            <select
              value={effectiveProjectId}
              onChange={(e) => setProjectId(e.target.value)}
              className="mb-2 w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-primary"
            >
              <option value="">{t('review.selectProject')}</option>
              {sharedProjects.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          )}
          <div className="mb-2 flex items-center gap-2">
            <StarRating value={stars} onChange={setStars} />
          </div>
          <textarea
            value={comment}
            onChange={(e) => setComment(e.target.value)}
            placeholder={t('marketTask.rateComment')}
            rows={2}
            className="mb-2 w-full resize-none rounded-lg border border-border bg-surface px-3 py-2 text-sm outline-none focus:border-primary"
          />
          {error && <p className="mb-2 text-sm text-red-600 dark:text-red-400">{error}</p>}
          <button
            type="button"
            onClick={submit}
            disabled={submitting || stars < 1 || !effectiveProjectId}
            className="rounded-lg bg-primary px-4 py-1.5 text-sm font-semibold text-white transition hover:bg-primary-hover disabled:opacity-50"
          >
            {t('marketTask.rateSubmit')}
          </button>
        </div>
      )}

      {reviews.length === 0 ? (
        <p className="text-sm text-muted">{t('profile.noReviews')}</p>
      ) : (
        <ul className="space-y-3">
          {reviews.map((r) => (
            <li key={r.id} className="rounded-lg border border-border p-3">
              <div className="flex items-center gap-2">
                <Avatar name={r.raterName} src={r.raterAvatarUrl} size={28} />
                <Link to={`/users/${r.raterId}`} className="text-sm font-medium hover:text-primary">
                  {r.raterName}
                </Link>
                <StarRating value={r.stars} />
                <span className="ml-auto flex-none text-xs text-muted">
                  {new Date(r.createdAt).toLocaleDateString()}
                </span>
              </div>
              <div className="mt-1.5">
                <ContextChip review={r} />
              </div>
              {r.comment && <p className="mt-2 whitespace-pre-wrap text-sm">{r.comment}</p>}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

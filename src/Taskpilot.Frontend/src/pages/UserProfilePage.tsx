import { useEffect, useState } from 'react'
import { ThumbsUp } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import Avatar from '../components/Avatar'
import StarRating from '../components/StarRating'
import ResultState from '../components/feedback/ResultState'
import { SkeletonDetail } from '../components/ui/Skeleton'
import { cn } from '../lib/cn'
import { forumService } from '../services/forumService'
import { userService, type PublicProfile, type ReputationEntry } from '../services/userService'
import { useAppSelector } from '../store/hooks'
import type { TopicListItem } from '../types/forum'

/** Emoji per reputation badge key (labels come from i18n). */
const BADGE_ICON: Record<string, string> = {
  solver: '🧩',
  contributor: '⬆️',
  freelancer: '💼',
  veteran: '🏆',
}

/** A contact line shown only when the value is present. */
function Contact({ label, value, href }: { label: string; value?: string | null; href?: string }) {
  if (!value) return null
  return (
    <div className="flex gap-2 text-sm">
      <span className="w-20 flex-none text-muted">{label}</span>
      {href ? (
        <a href={href} target="_blank" rel="noreferrer" className="truncate text-primary hover:underline">
          {value}
        </a>
      ) : (
        <span className="truncate">{value}</span>
      )}
    </div>
  )
}

/**
 * Public profile of any user (reached by clicking a name). Shows the safe profile
 * fields: name, role, title, location, bio, contacts and join date.
 */
export default function UserProfilePage() {
  const { t } = useTranslation()
  const { userId = '' } = useParams()
  const currentUserId = useAppSelector((s) => s.auth.user?.id)
  const [profile, setProfile] = useState<PublicProfile | null>(null)
  const [topics, setTopics] = useState<TopicListItem[]>([])
  const [reputation, setReputation] = useState<ReputationEntry[]>([])
  const [notFound, setNotFound] = useState(false)

  // You cannot endorse your own skills, so the buttons are hidden on your own profile.
  const isOwnProfile = !!currentUserId && profile?.id === currentUserId

  // Toggle an endorsement of one of this user's skills and apply the server's new count/state.
  const endorse = async (skill: string) => {
    const result = await userService.endorseSkill(userId, skill).catch(() => null)
    if (!result) return
    setProfile((prev) =>
      prev
        ? {
            ...prev,
            skillEndorsements: prev.skillEndorsements.map((s) =>
              s.skill === result.skill
                ? { ...s, count: result.count, endorsedByViewer: result.endorsed }
                : s,
            ),
          }
        : prev,
    )
  }

  useEffect(() => {
    if (!userId) return
    userService
      .getPublicProfile(userId)
      .then(setProfile)
      .catch(() => setNotFound(true))
    forumService.getTopics({ authorId: userId }).then((r) => setTopics(r.items)).catch(() => {})
    userService.getReputationHistory(userId).then((r) => setReputation(r.entries)).catch(() => {})
  }, [userId])

  return (
    <div className="mx-auto max-w-2xl px-6 py-8">
        {notFound ? (
          <ResultState variant="error" message={t('profile.notFound')} />
        ) : !profile ? (
          <SkeletonDetail />
        ) : (
          <>
            {/* Header */}
            <div className="rounded-xl border border-border bg-surface p-6">
              <div className="flex items-center gap-4">
                <Avatar name={profile.name} src={profile.avatarUrl} size={64} />
                <div className="min-w-0">
                  <h1 className="truncate text-2xl font-bold">{profile.name}</h1>
                  <div className="mt-1 flex items-center gap-2 text-sm text-muted">
                    <span className="rounded-full bg-canvas px-2 py-0.5 text-[11px] font-semibold">
                      {t(`admin.roles.${profile.role}`, profile.role)}
                    </span>
                    {profile.title && <span>{profile.title}</span>}
                  </div>
                  {profile.location && (
                    <div className="mt-1 text-sm text-muted">📍 {profile.location}</div>
                  )}
                  {profile.averageRating != null && (
                    <div className="mt-1 flex items-center gap-2 text-sm">
                      <StarRating value={Math.round(profile.averageRating)} />
                      <span className="text-muted">
                        {profile.averageRating} ({t('marketTask.reviews')}: {profile.reviewCount})
                      </span>
                    </div>
                  )}
                  <div className="mt-2 flex flex-wrap items-center gap-2">
                    <span className="rounded-full bg-primary px-2.5 py-0.5 text-xs font-bold text-white">
                      ⭐ {t('reputation.points', { count: profile.reputationPoints })}
                    </span>
                    {profile.badges.map((b) => (
                      <span
                        key={b}
                        title={t(`reputation.badge.${b}`, b)}
                        className="rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-semibold text-amber-800 dark:bg-amber-900/40 dark:text-amber-200"
                      >
                        {BADGE_ICON[b] ?? '🏅'} {t(`reputation.badge.${b}`, b)}
                      </span>
                    ))}
                  </div>
                </div>
              </div>

              <p className="mt-4 text-xs text-muted">
                {t('profile.memberSince', { date: new Date(profile.memberSince).toLocaleDateString() })}
              </p>
            </div>

            {/* Bio */}
            {profile.bio && (
              <div className="mt-6 rounded-xl border border-border bg-surface p-6">
                <h2 className="mb-2 font-bold">{t('settings.bio')}</h2>
                <p className="whitespace-pre-wrap text-sm">{profile.bio}</p>
              </div>
            )}

            {/* Skills (with peer endorsements) */}
            {profile.skills.length > 0 && (
              <div className="mt-6 rounded-xl border border-border bg-surface p-6">
                <h2 className="mb-3 font-bold">{t('settings.skills')}</h2>
                <div className="flex flex-wrap gap-2">
                  {profile.skills.map((skill) => {
                    const endorsement = profile.skillEndorsements.find((s) => s.skill === skill)
                    const count = endorsement?.count ?? 0
                    const mine = endorsement?.endorsedByViewer ?? false
                    return (
                      <span
                        key={skill}
                        className="flex items-center gap-1.5 rounded-full bg-primary/10 py-1 pl-3 pr-1.5 text-sm font-medium text-primary"
                      >
                        {skill}
                        {count > 0 && (
                          <span className="text-xs font-semibold text-primary/70 tabular-nums" title={t('endorse.count', { count })}>
                            {count}
                          </span>
                        )}
                        {isOwnProfile
                          ? count > 0 && <ThumbsUp className="h-3.5 w-3.5 text-primary/60" aria-hidden />
                          : (
                            <button
                              type="button"
                              onClick={() => endorse(skill)}
                              aria-pressed={mine}
                              aria-label={mine ? t('endorse.remove', { skill }) : t('endorse.add', { skill })}
                              title={mine ? t('endorse.remove', { skill }) : t('endorse.add', { skill })}
                              className={cn(
                                'flex h-6 w-6 items-center justify-center rounded-full transition',
                                mine
                                  ? 'bg-primary text-white'
                                  : 'text-primary hover:bg-primary/20',
                              )}
                            >
                              <ThumbsUp className="h-3.5 w-3.5" />
                            </button>
                          )}
                      </span>
                    )
                  })}
                </div>
              </div>
            )}

            {/* Contacts */}
            {(profile.email || profile.website || profile.linkedIn || profile.github || profile.phone) && (
              <div className="mt-6 rounded-xl border border-border bg-surface p-6">
                <h2 className="mb-3 font-bold">{t('profile.contacts')}</h2>
                <div className="space-y-2">
                  <Contact label={t('admin.email')} value={profile.email} href={profile.email ? `mailto:${profile.email}` : undefined} />
                  <Contact label={t('settings.website')} value={profile.website} href={profile.website ?? undefined} />
                  <Contact label={t('settings.linkedin')} value={profile.linkedIn} href={profile.linkedIn ?? undefined} />
                  <Contact label={t('settings.github')} value={profile.github} href={profile.github ?? undefined} />
                  <Contact label={t('settings.phone')} value={profile.phone} />
                </div>
              </div>
            )}

            {/* Reputation history (ledger) */}
            {reputation.length > 0 && (
              <div className="mt-6 rounded-xl border border-border bg-surface p-6">
                <h2 className="mb-3 font-bold">{t('reputation.history')}</h2>
                <ul className="space-y-1">
                  {reputation.map((e) => (
                    <li
                      key={e.id}
                      className="flex items-center gap-3 rounded-lg px-3 py-1.5 text-sm hover:bg-canvas/50"
                    >
                      <span
                        className={`w-12 flex-none text-right font-bold tabular-nums ${
                          e.delta >= 0 ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'
                        }`}
                      >
                        {e.delta > 0 ? `+${e.delta}` : e.delta}
                      </span>
                      <span className="min-w-0 flex-1 truncate">
                        {t(`reputation.reason.${e.reason}`, e.reason)}
                        {e.description && <span className="text-muted"> — {e.description}</span>}
                      </span>
                      <span className="flex-none text-xs text-muted">
                        {new Date(e.createdAt).toLocaleDateString()}
                      </span>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {/* Forum topics by this user */}
            <div className="mt-6 rounded-xl border border-border bg-surface p-6">
              <h2 className="mb-3 font-bold">{t('profile.topics')}</h2>
              {topics.length === 0 ? (
                <p className="text-sm text-muted">{t('profile.noTopics')}</p>
              ) : (
                <ul className="space-y-2">
                  {topics.map((topic) => (
                    <li key={topic.id}>
                      <Link
                        to={`/forum/${topic.id}`}
                        className="flex items-center gap-2 rounded-lg border border-border px-3 py-2 text-sm hover:bg-canvas/50"
                      >
                        {topic.isPinned && <span>📌</span>}
                        <span className="min-w-0 flex-1 truncate font-medium">{topic.title}</span>
                        <span className="flex-none text-xs text-muted">
                          {topic.replyCount} · {topic.viewCount}
                        </span>
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </>
        )}
      </div>
  )
}

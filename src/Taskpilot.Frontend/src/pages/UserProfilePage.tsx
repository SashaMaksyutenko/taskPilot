import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import Avatar from '../components/Avatar'
import Navbar from '../components/Navbar'
import StarRating from '../components/StarRating'
import { forumService } from '../services/forumService'
import { userService, type PublicProfile } from '../services/userService'
import type { TopicListItem } from '../types/forum'

/** A contact line shown only when the value is present. */
function Contact({ label, value, href }: { label: string; value?: string | null; href?: string }) {
  if (!value) return null
  return (
    <div className="flex gap-2 text-sm">
      <span className="w-20 flex-none text-slate-500 dark:text-slate-400">{label}</span>
      {href ? (
        <a href={href} target="_blank" rel="noreferrer" className="truncate text-[#1E2A44] hover:underline dark:text-slate-200">
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
  const [profile, setProfile] = useState<PublicProfile | null>(null)
  const [topics, setTopics] = useState<TopicListItem[]>([])
  const [notFound, setNotFound] = useState(false)

  useEffect(() => {
    if (!userId) return
    userService
      .getPublicProfile(userId)
      .then(setProfile)
      .catch(() => setNotFound(true))
    forumService.getTopics({ authorId: userId }).then((r) => setTopics(r.items)).catch(() => {})
  }, [userId])

  return (
    <div className="min-h-screen bg-slate-50 text-[#1E2A44] dark:bg-slate-900 dark:text-slate-100">
      <Navbar />
      <main className="mx-auto max-w-2xl px-6 py-8">
        {notFound ? (
          <p className="text-slate-400">{t('profile.notFound')}</p>
        ) : !profile ? (
          <p className="text-slate-400">{t('topic.loading')}</p>
        ) : (
          <>
            {/* Header */}
            <div className="rounded-xl border border-slate-200 bg-white p-6 dark:border-slate-700 dark:bg-slate-800">
              <div className="flex items-center gap-4">
                <Avatar name={profile.name} src={profile.avatarUrl} size={64} />
                <div className="min-w-0">
                  <h1 className="truncate text-2xl font-bold">{profile.name}</h1>
                  <div className="mt-1 flex items-center gap-2 text-sm text-slate-500 dark:text-slate-400">
                    <span className="rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-semibold dark:bg-slate-700">
                      {t(`admin.roles.${profile.role}`, profile.role)}
                    </span>
                    {profile.title && <span>{profile.title}</span>}
                  </div>
                  {profile.location && (
                    <div className="mt-1 text-sm text-slate-500 dark:text-slate-400">📍 {profile.location}</div>
                  )}
                  {profile.averageRating != null && (
                    <div className="mt-1 flex items-center gap-2 text-sm">
                      <StarRating value={Math.round(profile.averageRating)} />
                      <span className="text-slate-500 dark:text-slate-400">
                        {profile.averageRating} ({t('marketTask.reviews')}: {profile.reviewCount})
                      </span>
                    </div>
                  )}
                </div>
              </div>

              <p className="mt-4 text-xs text-slate-400">
                {t('profile.memberSince', { date: new Date(profile.memberSince).toLocaleDateString() })}
              </p>
            </div>

            {/* Bio */}
            {profile.bio && (
              <div className="mt-6 rounded-xl border border-slate-200 bg-white p-6 dark:border-slate-700 dark:bg-slate-800">
                <h2 className="mb-2 font-bold">{t('settings.bio')}</h2>
                <p className="whitespace-pre-wrap text-sm">{profile.bio}</p>
              </div>
            )}

            {/* Contacts */}
            {(profile.email || profile.website || profile.linkedIn || profile.github || profile.phone) && (
              <div className="mt-6 rounded-xl border border-slate-200 bg-white p-6 dark:border-slate-700 dark:bg-slate-800">
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

            {/* Forum topics by this user */}
            <div className="mt-6 rounded-xl border border-slate-200 bg-white p-6 dark:border-slate-700 dark:bg-slate-800">
              <h2 className="mb-3 font-bold">{t('profile.topics')}</h2>
              {topics.length === 0 ? (
                <p className="text-sm text-slate-400">{t('profile.noTopics')}</p>
              ) : (
                <ul className="space-y-2">
                  {topics.map((topic) => (
                    <li key={topic.id}>
                      <Link
                        to={`/forum/${topic.id}`}
                        className="flex items-center gap-2 rounded-lg border border-slate-200 px-3 py-2 text-sm hover:bg-slate-50 dark:border-slate-700 dark:hover:bg-slate-700/50"
                      >
                        {topic.isPinned && <span>📌</span>}
                        <span className="min-w-0 flex-1 truncate font-medium">{topic.title}</span>
                        <span className="flex-none text-xs text-slate-400">
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
      </main>
    </div>
  )
}

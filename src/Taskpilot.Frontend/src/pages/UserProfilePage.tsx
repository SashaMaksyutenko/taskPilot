import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useParams } from 'react-router-dom'
import Navbar from '../components/Navbar'
import { userService, type PublicProfile } from '../services/userService'

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
  const [notFound, setNotFound] = useState(false)

  useEffect(() => {
    if (!userId) return
    userService
      .getPublicProfile(userId)
      .then(setProfile)
      .catch(() => setNotFound(true))
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
                <div className="flex h-16 w-16 flex-none items-center justify-center rounded-full bg-[#1E2A44] text-2xl font-bold text-white">
                  {profile.name.charAt(0).toUpperCase()}
                </div>
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
          </>
        )}
      </main>
    </div>
  )
}
